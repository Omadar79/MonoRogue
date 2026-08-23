using Arch.Core;
using RogueSharp;
using SadRogue.Primitives;
using Point = SadRogue.Primitives.Point;

namespace MonoRogue.Core;

/// <summary>
/// Finds the next step from one cell toward another using RogueSharp's A* pathfinder.
/// Builds a RogueSharp <see cref="Map"/> on demand by mirroring the static terrain (walls
/// block movement) and treating <see cref="BlocksMovement"/> entities as obstacles. The
/// mover's own cell and the target cell are kept walkable so a path can start at the mover
/// and end at the target.
/// </summary>
public sealed class PathfindingService
{
    private readonly SpatialMap _spatial;
    private readonly World _world;
    private readonly QueryDescription _blockingEntities;

    public PathfindingService(SpatialMap spatial, World world)
    {
        _spatial = spatial;
        _world = world;
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
    }


    // Returns the first step along the shortest path from <paramref name="from"/> to <paramref name="to"/>, or 
    // <c>null</c> if no path exists. The step is always a cardinal (four-directional) neighbor of <paramref name="from"/>.
    public Point? GetNextStep(Point from, Point to)
    {
        var map = BuildMap(from, to);
        var pathFinder = new PathFinder(map);
        var path = pathFinder.TryFindShortestPath(map.GetCell(from.X, from.Y), map.GetCell(to.X, to.Y));
        if (path == null)
        {
            return null;
        }

        var next = path.TryStepForward();
        return next == null ? null : new Point(next.X, next.Y);
    }

    private Map BuildMap(Point from, Point to)
    {
        var tiles = _spatial.GetTileMap();
        var map = new Map(tiles.GetWidth(), tiles.GetHeight());

        for (int y = 0; y < tiles.GetHeight(); y++)
        {
            for (int x = 0; x < tiles.GetWidth(); x++)
            {
                var wall = tiles.GetTile(x, y) == TileKind.Wall;
                map.SetCellProperties(x, y, isTransparent: true, isWalkable: !wall);
            }
        }

        // Blocking entities are obstacles, except the mover's own cell and the target cell.
        _world.Query(in _blockingEntities, (ref Position pos) =>
        {
            if (pos.Value != from && pos.Value != to)
            {
                map.SetCellProperties(pos.Value.X, pos.Value.Y, isTransparent: true, isWalkable: false);
            }
        });

        return map;
    }
}
