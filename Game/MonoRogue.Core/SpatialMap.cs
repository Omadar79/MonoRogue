using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Owns map bounds, the static terrain grid (<see cref="TileMap"/>), and movement-blocking
/// lookups so movement systems can validate destinations without depending on <see cref="GameSession"/>.
/// </summary>
public sealed class SpatialMap
{
    private readonly World _world;
    private readonly QueryDescription _blockingEntities;
    private readonly TileMap _tiles;

    public SpatialMap(World world, int width, int height)
    {
        _world = world;
        _tiles = new TileMap(width, height);
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
    }

    public int GetWidth() => _tiles.GetWidth();
    public int GetHeight() => _tiles.GetHeight();
    public Point GetCenter() => new(_tiles.GetWidth() / 2, _tiles.GetHeight() / 2);

    /// <summary>The static terrain grid backing this map.</summary>
    public TileMap GetTileMap() => _tiles;

    public bool IsValidCell(Point position) => _tiles.Contains(position);

    public bool IsBlocked(Point position)
    {
        // Walls and out-of-bounds cells are impassable regardless of entities.
        if (!_tiles.Contains(position) || !_tiles.IsWalkable(position))
        {
            return true;
        }

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
