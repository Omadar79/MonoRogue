using Arch.Core;
using SadConsole;
using SadRogue.Primitives;
using Color = SadRogue.Primitives.Color;

namespace MonoRogue.Core;

public class MapBase : IDisposable
{
    private World _world;

    // Query descriptions for filtering entities based on their components
    private readonly QueryDescription _blockingEntities;

    private readonly QueryDescription _renderableEntities;

    private Player _player = null!;
    private ScreenSurface _mapSurface = null!;

    public World World => _world; 
    public ScreenSurface SurfaceObject => _mapSurface; 

    public MapBase(int mapWidth, int mapHeight)
    {
        _world = World.Create();
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();

        _mapSurface = new ScreenSurface(mapWidth, mapHeight);

        _mapSurface.UseMouse = false;
        // Create the player entity and then render the initial surface
        _player = new Player(_world, this);

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

    // Load map data into the current world. This will clear the existing world and recreate entities
    // according to the supplied MapData. Minimal behavior: colors are defaulted and player is recreated
    // if present in the DTO.
    public void LoadMap(MapData mapData, IGlyphMapper? mapper = null)
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
                _world.Create(pos, glyph, new BlocksMovement());
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
                result = g is not null ? new PlayerState(pos.Value.X, pos.Value.Y, g) : null;
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

    // Create a player entity in the current world using a PlayerState DTO.
    public void CreatePlayerFromState(PlayerState state, IGlyphMapper? mapper = null)
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
    public void ClearWorld(bool preservePlayerState = false)
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

    public void GenerateNewMap()
    {

        //_world.Clear();

        FillBackground();

        CreateTreasure();
        CreateMonster();

        

        RefreshSurface();
    }

    public bool TryMovePlayer(Point offset)
    {
        if (_player == null) return false;

        return _player.TryMovePlayer(offset);
    }

    public void Dispose()
    {
        _world.Dispose();
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

    private void CreateMonster()
    {
        for (int i = 0; i < 1000; i++)
        {
            Point randomPosition = new Point(Game.Instance.Random.Next(0, _mapSurface.Surface.Width),Game.Instance.Random.Next(0, _mapSurface.Surface.Height));

            if (IsBlocked(randomPosition)) continue;

            _world.Create(new Position(randomPosition), new RenderGlyph(new ColoredGlyph(Color.Red, Color.Black, 'M')), new BlocksMovement());

            break;
        }
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

}