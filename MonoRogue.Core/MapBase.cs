using Arch.Core;
using SadConsole;
using SadRogue.Primitives;
using Color = SadRogue.Primitives.Color;

namespace MonoRogue.Core;

public class MapBase : IDisposable
{
    private readonly World _world;

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