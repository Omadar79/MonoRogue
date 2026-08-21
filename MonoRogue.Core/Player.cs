using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Core;

public class Player
{
    private const int ArgbWhite = unchecked((int)0xFFFFFFFF);
    private const int ArgbBlack = unchecked((int)0xFF000000);

    private readonly QueryDescription _playerEntities;

    private readonly World _world;

    private readonly MapBase _currentMap;

    public Player(World world, MapBase currentMap)
    {
        _world = world;
        _playerEntities = new QueryDescription().WithAll<Position, PlayerControlled, RenderGlyph>();

        _currentMap = currentMap;

        CreatePlayer(_currentMap.GetMapCenter());
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

        return moved;
    }
    
    private void CreatePlayer(Point position)
    {
        _world.Create(new Position(position), RenderGlyph.FromArgb('@', ArgbWhite, ArgbBlack), new PlayerControlled(), new BlocksMovement());
    }
}


