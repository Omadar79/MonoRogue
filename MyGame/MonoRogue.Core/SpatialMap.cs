using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Owns map bounds and movement-blocking lookups so movement systems can validate
/// destinations without depending on <see cref="MapBase"/>.
/// </summary>
public sealed class SpatialMap
{
    private readonly World _world;
    private readonly QueryDescription _blockingEntities;

    public int Width { get; }
    public int Height { get; }
    public Point Center => new(Width / 2, Height / 2);

    public SpatialMap(World world, int width, int height)
    {
        _world = world;
        Width = width;
        Height = height;
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
    }

    public bool IsValidCell(Point position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

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
