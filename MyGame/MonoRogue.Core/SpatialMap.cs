using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Owns map bounds and movement-blocking lookups so movement systems can validate
/// destinations without depending on <see cref="GameSession"/>.
/// </summary>
public sealed class SpatialMap
{
    private readonly World _world;
    private readonly QueryDescription _blockingEntities;
    private readonly int _width;
    private readonly int _height;

    public SpatialMap(World world, int width, int height)
    {
        _world = world;
        _width = width;
        _height = height;
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
    }

    public int GetWidth() => _width;
    public int GetHeight() => _height;
    public Point GetCenter() => new(_width / 2, _height / 2);

    public bool IsValidCell(Point position) =>
        position.X >= 0 && position.Y >= 0 && position.X < _width && position.Y < _height;

    public bool IsBlocked(Point position)
    {
        var blocked = false;
        _world.Query(in _blockingEntities, (ref Position other) =>
        {
            if (other.Value == position)
            {
                blocked = true;
            }
        });

        return blocked;
    }

    public bool CanOccupy(Point position) => IsValidCell(position) && !IsBlocked(position);
}
