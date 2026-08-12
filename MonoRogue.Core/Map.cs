using Arch.Core;
using SadConsole;
using SadRogue.Primitives;
using Color = SadRogue.Primitives.Color;


namespace MonoRogue.Core;

public sealed class Map : IDisposable
{
    private readonly World _world;
    private readonly QueryDescription _blockingEntities;
    private readonly QueryDescription _playerEntities;
    private readonly QueryDescription _renderableEntities;
    private ScreenSurface _mapSurface;

    public World World => _world;
    public ScreenSurface SurfaceObject => _mapSurface;

    public Map(int mapWidth, int mapHeight)
    {
        _world = World.Create();
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
        _playerEntities = new QueryDescription().WithAll<Position, PlayerControlled, RenderGlyph>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
        _mapSurface = new ScreenSurface(mapWidth, mapHeight);
        _mapSurface.UseMouse = false;

        FillBackground();

        CreatePlayer(_mapSurface.Surface.Area.Center);

        CreateTreasure();
        CreateMonster();

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

    private void CreatePlayer(Point position)
    {
        _world.Create(new Position(position), new RenderGlyph(new ColoredGlyph(Color.White, Color.Black, 2)), new PlayerControlled(), new BlocksMovement());
    }

    private void CreateTreasure()
    {
        for (int i = 0; i < 1000; i++)
        {
            Point randomPosition = new Point(Game.Instance.Random.Next(0, _mapSurface.Surface.Width),
                                             Game.Instance.Random.Next(0, _mapSurface.Surface.Height));

            if (IsBlocked(randomPosition)) continue;

            _world.Create(new Position(randomPosition), new RenderGlyph(new ColoredGlyph(Color.Yellow, Color.Black, 'v')), new BlocksMovement());
            break;
        }
    }

    private void CreateMonster()
    {
        for (int i = 0; i < 1000; i++)
        {
            Point randomPosition = new Point(Game.Instance.Random.Next(0, _mapSurface.Surface.Width),
                                                Game.Instance.Random.Next(0, _mapSurface.Surface.Height));

            if (IsBlocked(randomPosition)) continue;

            _world.Create(new Position(randomPosition), new RenderGlyph(new ColoredGlyph(Color.Red, Color.Black, 'M')), new BlocksMovement());
            break;
        }
    }

    public bool TryMovePlayer(Point offset)
    {
        var moved = false;

        _world.Query(in _playerEntities, (ref Position position, ref RenderGlyph renderGlyph) =>
        {
            var destination = position.Value + offset;
            if (!IsValidCell(destination) || IsBlocked(destination))
            {
                return;
            }

            position.Value = destination;
            moved = true;
        });

        if (moved)
        {
            RefreshSurface();
        }

        return moved;
    }

    private bool IsValidCell(Point position)
    {
        return _mapSurface.IsValidCell(position.X, position.Y);
    }

    private bool IsBlocked(Point position)
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

    private void RefreshSurface()
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

    public void Dispose()
    {
        _world.Dispose();
    }
}