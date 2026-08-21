using Arch.Core;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

public class MapBase : IDisposable
{
    private World _world;

    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private readonly QueryDescription _blockingEntities;
    private readonly QueryDescription _renderableEntities;
    private readonly QueryDescription _monsterActors;
    private const int DefaultEnergyPerTurn = 100;
    private const int DefaultActionCost = 100;
    private const int MaxMonsterActionsPerTurn = 4;
    private const int ArgbBlack = unchecked((int)0xFF000000);
    private const int ArgbWhite = unchecked((int)0xFFFFFFFF);
    private const int ArgbRed = unchecked((int)0xFFFF0000);
    private const int ArgbYellow = unchecked((int)0xFFFFFF00);
    private const int ArgbOrangeRed = unchecked((int)0xFFFF4500);

    public World World => _world;
    public int Width => _mapWidth;
    public int Height => _mapHeight;

    public MapBase(int mapWidth, int mapHeight)
    {
        _world = World.Create();
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
        _monsterActors = new QueryDescription().WithAll<Position, MonsterControlled, Energy, RenderGlyph>();

        CreateInitialPlayer();
        GenerateNewMap();
    }

    // Save current map data (entities + basic flags) into a UI-agnostic MapData DTO.
    public MapData SaveMap()
    {
        var entities = new List<EntityDTO>();

        var blockingPositions = new HashSet<Point>();
        _world.Query(in _blockingEntities, (ref Position pos) => { blockingPositions.Add(pos.Value); });

        var playerPositions = new HashSet<Point>();
        var playerQuery = new QueryDescription().WithAll<Position, PlayerControlled>();
        _world.Query(in playerQuery, (ref Position pos) => { playerPositions.Add(pos.Value); });

        _world.Query(in _renderableEntities, (ref Position pos, ref RenderGlyph glyph) =>
        {
            var glyphDto = new GlyphDTO(glyph.Value.Glyph, glyph.Value.ForegroundArgb, glyph.Value.BackgroundArgb);

            var isBlocked = blockingPositions.Contains(pos.Value);
            var isPlayer = playerPositions.Contains(pos.Value);
            entities.Add(new EntityDTO(pos.Value.X, pos.Value.Y, glyphDto, isBlocked, isPlayer));
        });

        return new MapData(_mapWidth, _mapHeight, entities);
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    // Load map data into the current world. This clears and recreates entities from the DTO.
    public void LoadMap(MapData? mapData)
    {
        if (mapData == null)
        {
            return;
        }

        _world.Dispose();
        _world = World.Create();

        foreach (var e in mapData.Entities)
        {
            var pos = new Position(new Point(e.X, e.Y));
            var glyph = new RenderGlyph(new CoreGlyph(e.Glyph.Glyph, e.Glyph.ForegroundArgb, e.Glyph.BackgroundArgb));

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
    }

    // Extract a minimal PlayerState from the world (first found player entity)
    public PlayerState? ExtractPlayerState()
    {
        PlayerState? result = null;
        var q = new QueryDescription().WithAll<Position, PlayerControlled, RenderGlyph>();
        _world.Query(in q, (ref Position pos, ref RenderGlyph glyph) =>
        {
            var glyphDto = new GlyphDTO(glyph.Value.Glyph, glyph.Value.ForegroundArgb, glyph.Value.BackgroundArgb);
            result = new PlayerState(pos.Value.X, pos.Value.Y, glyphDto);
        });

        return result;
    }

    public bool TryMovePlayer(Point offset)
    {
        return TryMovePlayerNoRefresh(offset);
    }

    // Process one gameplay turn in deterministic order: player action -> monster actions.
    public TurnResult ProcessPlayerTurn(Point playerDelta)
    {
        var playerMoved = TryMovePlayerNoRefresh(playerDelta);
        var monsterActionsExecuted = ProcessMonsters();

        return new TurnResult(playerMoved, monsterActionsExecuted);
    }

    public bool IsValidCell(Point position)
    {
        return position.X >= 0 && position.Y >= 0 && position.X < _mapWidth && position.Y < _mapHeight;
    }

    public Point GetMapCenter()
    {
        return new Point(_mapWidth / 2, _mapHeight / 2);
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

    private MonsterActionPlan PlanMonsterAction(Point monsterPosition, Point playerPosition, CoreGlyph glyph, Energy energy)
    {
        var delta = playerPosition - monsterPosition;
        var stepX = Math.Sign(delta.X);
        var stepY = Math.Sign(delta.Y);
        var distance = Math.Abs(delta.X) + Math.Abs(delta.Y);

        var glyphChar = glyph.Glyph;
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
    }

    private void CreateItem(ItemDefinition definition)
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb));

            break;
        }
    }

    private void CreateMonster(MonsterDefinition definition)
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb),
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
    private void CreatePlayerFromState(PlayerState? state)
    {
        if (state == null)
        {
            return;
        }

        var pos = new Position(new Point(state.X, state.Y));
        var glyph = new RenderGlyph(new CoreGlyph(state.Glyph.Glyph, state.Glyph.ForegroundArgb, state.Glyph.BackgroundArgb));

        _world.Create(pos, glyph, new PlayerControlled(), new BlocksMovement());
    }

    // Clear the current world. If preservePlayerState is true, player will be extracted and recreated after the world is cleared.
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
    }

    private void CreateInitialPlayer()
    {
        var center = new Point(_mapWidth / 2, _mapHeight / 2);
        _world.Create(new Position(center),
            RenderGlyph.FromArgb('@', ArgbWhite, ArgbBlack),
            new PlayerControlled(),
            new BlocksMovement());
    }

    private void CreateTreasure()
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition), RenderGlyph.FromArgb('v', ArgbYellow, ArgbBlack), new BlocksMovement());
            break;
        }
    }

    private void CreateGoblin()
    {
        CreateMonster('g', ArgbRed, DefaultEnergyPerTurn, DefaultActionCost);
    }

    private void CreateDragon()
    {
        // Dragon gains energy like a normal actor but can spend more on special actions.
        CreateMonster('D', ArgbOrangeRed, DefaultEnergyPerTurn, DefaultActionCost);
    }

    private void CreateMonster(int glyphCode, int foregroundArgb, int gainPerTurn, int actionCost)
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb((char)glyphCode, foregroundArgb, ArgbBlack),
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
