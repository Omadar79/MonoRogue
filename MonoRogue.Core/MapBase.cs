using Arch.Core;
using MonoRogue.Data;
using SadConsole;
using SadRogue.Primitives;
using Color = SadRogue.Primitives.Color;

namespace MonoRogue.Core;

public class MapBase : IDisposable
{
    private World _world;
    
    private readonly QueryDescription _blockingEntities;
    private readonly QueryDescription _renderableEntities;
    private readonly QueryDescription _monsterActors;
    private readonly ScreenSurface _mapSurface;
    private const int DefaultEnergyPerTurn = 100;
    private const int DefaultActionCost = 100;
    private const int MaxMonsterActionsPerTurn = 4;

    public World World => _world; 
    public ScreenSurface SurfaceObject => _mapSurface; 

    public MapBase(int mapWidth, int mapHeight)
    {
        _world = World.Create();
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
        _monsterActors = new QueryDescription().WithAll<Position, MonsterControlled, Energy, RenderGlyph>();

        _mapSurface = new ScreenSurface(mapWidth, mapHeight);

        _mapSurface.UseMouse = false;
        CreateInitialPlayer();

        GenerateNewMap();    
    }

    // --- Persistence / transfer API (stubs / minimal implementation) ---
    // Save current map data (entities + basic flags) into a UI-agnostic MapData DTO.
    public MapData SaveMap(IGlyphMapper? mapper = null)
    {
        var entities = new List<EntityDTO>();

        // Collect blocking positions
        var blockingPositions = new HashSet<Point>();
        _world.Query(in _blockingEntities, (ref Position pos) => { blockingPositions.Add(pos.Value); });

        // Collect player positions
        var playerPositions = new HashSet<Point>();
        var playerQuery = new QueryDescription().WithAll<Position, PlayerControlled>();
        _world.Query(in playerQuery, (ref Position pos) => { playerPositions.Add(pos.Value); });

        // Collect renderable entities
        _world.Query(in _renderableEntities, (ref Position pos, ref RenderGlyph glyph) =>
        {
            GlyphDTO glyphDTO;
            if (mapper != null)
            {
                glyphDTO = mapper.ToGlyphDTO(glyph.Value);
            }
            else
            {
                var glyphChar = (char)glyph.Value.Glyph;
                var fg = glyph.Value.Foreground;
                var bg = glyph.Value.Background;
                var fgArgb = ColorConverter.ToArgb(fg);
                var bgArgb = ColorConverter.ToArgb(bg);
                glyphDTO = new GlyphDTO(glyphChar, fgArgb, bgArgb);
            }

            var isBlock = blockingPositions.Contains(pos.Value);
            var isPlayer = playerPositions.Contains(pos.Value);

            entities.Add(new EntityDTO(pos.Value.X, pos.Value.Y, glyphDTO, isBlock, isPlayer));
        });

        return new MapData(_mapSurface.Surface.Width, _mapSurface.Surface.Height, entities);
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    // Load map data into the current world. This will clear the existing world and recreate entities
    // according to the supplied MapData. Minimal behavior: colors are defaulted and player is recreated
    // if present in the DTO.
    public void LoadMap(MapData? mapData, IGlyphMapper? mapper = null)
    {
        if (mapData == null) return;

        // Dispose old world and create a fresh one.
        _world.Dispose();
        _world = World.Create();

        // Recreate entities from DTO
        foreach (var e in mapData.Entities)
        {
            var pos = new Position(new Point(e.X, e.Y));
            RenderGlyph glyph;
            if (mapper != null)
            {
                var cg = mapper.ToColoredGlyph(e.Glyph);
                glyph = new RenderGlyph(cg);
            }
            else
            {
                var fg = ColorConverter.FromArgb(e.Glyph.ForegroundArgb);
                var bg = ColorConverter.FromArgb(e.Glyph.BackgroundArgb);
                glyph = new RenderGlyph(new ColoredGlyph(fg, bg, e.Glyph.Glyph));
            }

            if (e.IsPlayer)
            {
                _world.Create(pos, glyph, new PlayerControlled(), new BlocksMovement());
            }
            else if (e.BlocksMovement)
            {
                if (e.Glyph.Glyph is 'M' or 'g' or 'D')
                {
                    _world.Create(pos,
                        glyph,
                        new BlocksMovement(),
                        new MonsterControlled(),
                        new Energy { Current = 0, GainPerTurn = DefaultEnergyPerTurn, ActionCost = DefaultActionCost });
                }
                else
                {
                    _world.Create(pos, glyph, new BlocksMovement());
                }
            }
            else
            {
                _world.Create(pos, glyph);
            }
        }

        RefreshSurface();
    }

    // Extract a minimal PlayerState from the world (first found player entity)
    public PlayerState? ExtractPlayerState(IGlyphMapper? mapper = null)
    {
        PlayerState? result = null;
        var q = new QueryDescription().WithAll<Position, PlayerControlled, RenderGlyph>();
        _world.Query(in q, (ref Position pos, ref RenderGlyph glyph) =>
        {
            if (mapper != null)
            {
                var g = mapper.ToGlyphDTO(glyph.Value);
                result = new PlayerState(pos.Value.X, pos.Value.Y, g);
            }
            else
            {
                var ch = (char)glyph.Value.Glyph;
                var fgArgb = ColorConverter.ToArgb(glyph.Value.Foreground);
                var bgArgb = ColorConverter.ToArgb(glyph.Value.Background);
                result = new PlayerState(pos.Value.X, pos.Value.Y, new GlyphDTO(ch, fgArgb, bgArgb));
            }
        });

        return result;
    }

    public bool TryMovePlayer(Point offset)
    {
        var moved = TryMovePlayerNoRefresh(offset);
        if (moved)
        {
            RefreshSurface();
        }

        return moved;
    }

    // Process one gameplay turn in deterministic order: player action -> monster actions -> one refresh.
    public TurnResult ProcessPlayerTurn(Point playerDelta)
    {
        var playerMoved = TryMovePlayerNoRefresh(playerDelta);
        var monsterActionsExecuted = ProcessMonsters();

        RefreshSurface();
        return new TurnResult(playerMoved, monsterActionsExecuted);
    }
    

    public bool IsValidCell(Point position)
    {
        return _mapSurface.IsValidCell(position.X, position.Y);
    }

    public bool IsBlocked(Point position)
    {
        var blocked = false;

        _world.Query(in _blockingEntities, (ref Position otherPosition) =>
        {
            if (otherPosition.Value == position)
            {
                blocked = true;
            }
        });

        return blocked;
    }

    public void RefreshSurface()
    {
        FillBackground();

        _world.Query(in _renderableEntities, (ref Position position, ref RenderGlyph glyph) =>
        {
            if (IsValidCell(position.Value))
            {
                glyph.Value.CopyAppearanceTo(_mapSurface.Surface[position.Value]);
            }
        });

        _mapSurface.IsDirty = true;
    }

    private bool TryMovePlayerNoRefresh(Point offset)
    {
        var moved = false;
        var playerQuery = new QueryDescription().WithAll<Position, PlayerControlled>();
        _world.Query(in playerQuery, (ref Position position) =>
        {
            var destination = position.Value + offset;
            if (!IsValidCell(destination) || IsBlocked(destination))
            {
                return;
            }

            position.Value = destination;
            moved = true;
        });

        return moved;
    }

    private int ProcessMonsters()
    {
        var playerPosition = GetPlayerPosition();
        if (playerPosition is null)
        {
            return 0;
        }

        var actionsExecuted = 0;
        _world.Query(in _monsterActors, (ref Position monsterPosition, ref Energy energy, ref RenderGlyph glyph) =>
        {
            if (energy.GainPerTurn <= 0)
            {
                return;
            }

            energy.Current += energy.GainPerTurn;

            var actionsForThisMonster = 0;
            while (actionsForThisMonster < MaxMonsterActionsPerTurn)
            {
                var action = PlanMonsterAction(monsterPosition.Value, playerPosition.Value, glyph.Value, energy);
                if (action.EnergyCost <= 0 || energy.Current < action.EnergyCost)
                {
                    break;
                }

                if (ExecuteMonsterAction(ref monsterPosition, action, playerPosition.Value))
                {
                    actionsExecuted++;
                }

                energy.Current -= action.EnergyCost;
                actionsForThisMonster++;
            }
        });

        return actionsExecuted;
    }

    private MonsterActionPlan PlanMonsterAction(Point monsterPosition, Point playerPosition, ColoredGlyph glyph, Energy energy)
    {
        var delta = playerPosition - monsterPosition;
        var stepX = Math.Sign(delta.X);
        var stepY = Math.Sign(delta.Y);
        var distance = Math.Abs(delta.X) + Math.Abs(delta.Y);

        var glyphChar = (char)glyph.Glyph;
        if (glyphChar == 'D' && distance <= 3)
        {
            return new MonsterActionPlan(MonsterActionType.BreathAttack, Point.None, 300);
        }

        if (stepX != 0)
        {
            return new MonsterActionPlan(MonsterActionType.StepTowardPlayer, new Point(stepX, 0), Math.Max(1, energy.ActionCost));
        }

        if (stepY != 0)
        {
            return new MonsterActionPlan(MonsterActionType.StepTowardPlayer, new Point(0, stepY), Math.Max(1, energy.ActionCost));
        }

        return new MonsterActionPlan(MonsterActionType.Wait, Point.None, Math.Max(1, energy.ActionCost));
    }

    private bool ExecuteMonsterAction(ref Position monsterPosition, MonsterActionPlan action, Point playerPosition)
    {
        switch (action.Type)
        {
            case MonsterActionType.StepTowardPlayer:
            {
                var candidate = monsterPosition.Value + action.Delta;
                if (CanMonsterOccupy(candidate, monsterPosition.Value))
                {
                    monsterPosition.Value = candidate;
                    return true;
                }

                return false;
            }
            case MonsterActionType.BreathAttack:
                // Stub: combat/resistance pipeline will be added later.
                return IsInBreathRange(monsterPosition.Value, playerPosition, 3);
            case MonsterActionType.Wait:
            default:
                return false;
        }
    }

    private static bool IsInBreathRange(Point origin, Point target, int range)
    {
        var distance = Math.Abs(origin.X - target.X) + Math.Abs(origin.Y - target.Y);
        return distance <= range;
    }

    private Point? GetPlayerPosition()
    {
        Point? result = null;
        var playerQuery = new QueryDescription().WithAll<Position, PlayerControlled>();
        _world.Query(in playerQuery, (ref Position position) =>
        {
            result ??= position.Value;
        });

        return result;
    }

    private bool CanMonsterOccupy(Point destination, Point currentPosition)
    {
        if (!IsValidCell(destination) || destination == currentPosition)
        {
            return false;
        }

        var blocked = false;
        _world.Query(in _blockingEntities, (ref Position otherPosition) =>
        {
            if (otherPosition.Value == destination)
            {
                blocked = true;
            }
        });

        return !blocked;
    }

    private void GenerateNewMap()
    {
        //_world.Clear();
        FillBackground();

        CreateTreasure();

        var itemTemplates = ItemDataLoader.LoadDefinitionsFromDefaultSearchPaths(Directory.GetCurrentDirectory());
        foreach (var template in itemTemplates)
        {
            CreateItem(template);
        }

        var templates = MonsterDataLoader.LoadDefinitionsFromDefaultSearchPaths(Directory.GetCurrentDirectory());
        if (templates.Count == 0)
        {
            CreateGoblin();
            CreateDragon();
        }
        else
        {
            foreach (var template in templates)
            {
                CreateMonster(template);
            }
        }

        RefreshSurface();
    }

    private void CreateItem(ItemDefinition definition)
    {
        var foreground = ColorConverter.FromArgb(definition.ForegroundArgb);
        var background = ColorConverter.FromArgb(definition.BackgroundArgb);

        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Game.Instance.Random.Next(0, _mapSurface.Surface.Width), Game.Instance.Random.Next(0, _mapSurface.Surface.Height));

            if (IsBlocked(randomPosition)) continue;

            _world.Create(new Position(randomPosition),
                new RenderGlyph(new ColoredGlyph(foreground, background, definition.Glyph)));

            break;
        }
    }

    private void CreateMonster(MonsterDefinition definition)
    {
        var foreground = ColorConverter.FromArgb(definition.ForegroundArgb);
        var background = ColorConverter.FromArgb(definition.BackgroundArgb);

        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Game.Instance.Random.Next(0, _mapSurface.Surface.Width), Game.Instance.Random.Next(0, _mapSurface.Surface.Height));

            if (IsBlocked(randomPosition)) continue;

            _world.Create(new Position(randomPosition),
                new RenderGlyph(new ColoredGlyph(foreground, background, definition.Glyph)),
                new BlocksMovement(),
                new MonsterControlled(),
                new Energy
                {
                    Current = 0,
                    GainPerTurn = Math.Max(1, definition.GainPerTurn),
                    ActionCost = Math.Max(1, definition.ActionCost)
                });

            break;
        }
    }

    // Create a player entity in the current world using a PlayerState DTO.
    private void CreatePlayerFromState(PlayerState? state, IGlyphMapper? mapper = null)
    {
        if (state == null) return;

        var pos = new Position(new Point(state.X, state.Y));
        RenderGlyph glyph;
        if (mapper != null)
        {
            glyph = new RenderGlyph(mapper.ToColoredGlyph(state.Glyph));
        }
        else
        {
            var fg = ColorConverter.FromArgb(state.Glyph.ForegroundArgb);
            var bg = ColorConverter.FromArgb(state.Glyph.BackgroundArgb);
            glyph = new RenderGlyph(new ColoredGlyph(fg, bg, state.Glyph.Glyph));
        }

        _world.Create(pos, glyph, new PlayerControlled(), new BlocksMovement());
        RefreshSurface();
    }

    // Clear the current world. If preservePlayerState is true, player will be extracted and recreated
    // after the world is cleared.
    private void ClearWorld(bool preservePlayerState = false)
    {
        PlayerState? saved = null;
        if (preservePlayerState)
        {
            saved = ExtractPlayerState();
        }

        _world.Dispose();
        _world = World.Create();

        if (saved != null)
        {
            CreatePlayerFromState(saved);
        }

        RefreshSurface();
    }



  
    private void FillBackground()
    {
        Color[] colors = new[] { Color.LightGreen, Color.Coral, Color.CornflowerBlue, Color.DarkGreen };
        float[] colorStops = new[] { 0f, 0.35f, 0.75f, 1f };

        Algorithms.GradientFill(_mapSurface.FontSize,
                                _mapSurface.Surface.Area.Center,
                                _mapSurface.Surface.Width / 3,
                                45,
                                _mapSurface.Surface.Area,
                                new Gradient(colors, colorStops),
                                (x, y, color) => _mapSurface.Surface[x, y].Background = color);
    }

    private void CreateInitialPlayer()
    {
        var center = _mapSurface.Surface.Area.Center;
        _world.Create(new Position(center),
            new RenderGlyph(new ColoredGlyph(Color.White, Color.Black, '@')),
            new PlayerControlled(),
            new BlocksMovement());
    }

    
    private void CreateTreasure()
    {
        for (int i = 0; i < 1000; i++)
        {
            Point randomPosition = new Point(Game.Instance.Random.Next(0, _mapSurface.Surface.Width), Game.Instance.Random.Next(0, _mapSurface.Surface.Height));

            if (IsBlocked(randomPosition)) continue;

            _world.Create(new Position(randomPosition), new RenderGlyph(new ColoredGlyph(Color.Yellow, Color.Black, 'v')), new BlocksMovement());
            break;
        }
    }

    private void CreateGoblin()
    {
        CreateMonster('g', Color.Red, DefaultEnergyPerTurn, DefaultActionCost);
    }

    private void CreateDragon()
    {
        // Dragon gains energy like a normal actor but can spend more on special actions.
        CreateMonster('D', Color.OrangeRed, DefaultEnergyPerTurn, DefaultActionCost);
    }

    private void CreateMonster(int glyphCode, Color foreground, int gainPerTurn, int actionCost)
    {
        for (int i = 0; i < 1000; i++)
        {
            Point randomPosition = new Point(Game.Instance.Random.Next(0, _mapSurface.Surface.Width),Game.Instance.Random.Next(0, _mapSurface.Surface.Height));

            if (IsBlocked(randomPosition)) continue;

            _world.Create(new Position(randomPosition),
                new RenderGlyph(new ColoredGlyph(foreground, Color.Black, glyphCode)),
                new BlocksMovement(),
                new MonsterControlled(),
                new Energy
                {
                    Current = 0,
                    GainPerTurn = Math.Max(1, gainPerTurn),
                    ActionCost = Math.Max(1, actionCost)
                });

            break;
        }
    }
}