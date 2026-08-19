using Arch.Core;
using SadConsole;
using SadRogue.Primitives;
using Color = SadRogue.Primitives.Color;

namespace MonoRogue.Core;

public class Player
{
    private readonly QueryDescription _playerEntities;

    private readonly World _world;

    private readonly MapBase _currentMap;

    public Player(World world, MapBase currentMap)
    {
        _world = world;
        _playerEntities = new QueryDescription().WithAll<Position, PlayerControlled, RenderGlyph>();

        _currentMap = currentMap;

        CreatePlayer(_currentMap.SurfaceObject.Surface.Area.Center);
    }
    
    public bool TryMovePlayer(Point offset)
    {
        var moved = false;

        _world.Query(in _playerEntities, (ref Position position, ref RenderGlyph renderGlyph) =>
        {
            var destination = position.Value + offset;
            if (!_currentMap.IsValidCell(destination) || _currentMap.IsBlocked(destination))
            {
                return;
            }

            position.Value = destination;
            moved = true;
        });

        if (moved)
        {
            _currentMap.RefreshSurface();
        }

        return moved;
    }
    
    private void CreatePlayer(Point position)
    {
        _world.Create(new Position(position), new RenderGlyph(new ColoredGlyph(Color.White, Color.Black, '@')), new PlayerControlled(), new BlocksMovement());
    }
}


