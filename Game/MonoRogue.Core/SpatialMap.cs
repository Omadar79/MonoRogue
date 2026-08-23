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
        : this(world, new TileMap(width, height))
    {
    }

    public SpatialMap(World world, TileMap tiles)
    {
        _world = world;
        _tiles = tiles;
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

    /// <summary>
    /// Returns a uniformly random walkable floor cell, or <c>null</c> if the map has no
    /// floor tiles. Guarantees the result is never a wall; used to place non-blocking
    /// entities such as items.
    /// </summary>
    public Point? GetRandomWalkableCell(Random rng)
    {
        return PickRandomCell(rng, _tiles.IsWalkable);
    }

    /// <summary>
    /// Returns a uniformly random cell that is both walkable and not currently blocked by
    /// an entity, or <c>null</c> if none exists. Used to place blocking entities (player,
    /// monsters, obstacles) without overlapping each other.
    /// </summary>
    public Point? GetRandomOpenCell(Random rng)
    {
        return PickRandomCell(rng, CanOccupy);
    }

    private Point? PickRandomCell(Random rng, Func<Point, bool> predicate)
    {
        var candidates = new List<Point>();
        for (int y = 0; y < GetHeight(); y++)
        {
            for (int x = 0; x < GetWidth(); x++)
            {
                var position = new Point(x, y);
                if (predicate(position))
                {
                    candidates.Add(position);
                }
            }
        }

        return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
    }
}
