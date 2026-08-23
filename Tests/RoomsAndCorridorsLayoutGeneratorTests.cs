using MonoRogue.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class RoomsAndCorridorsLayoutGeneratorTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalLayout()
    {
        var generator = new RoomsAndCorridorsLayoutGenerator();
        var a = generator.Generate(40, 30, seed: 1234);
        var b = generator.Generate(40, 30, seed: 1234);

        for (int y = 0; y < 30; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                if (a.GetTile(x, y) != b.GetTile(x, y))
                {
                    throw new InvalidOperationException($"Tile ({x},{y}) differs between identical seeds.");
                }
            }
        }
    }

    [Fact]
    public void BorderIsAlwaysWall()
    {
        var generator = new RoomsAndCorridorsLayoutGenerator();
        var tiles = generator.Generate(50, 40, seed: 99);

        for (int x = 0; x < 50; x++)
        {
            if (tiles.GetTile(x, 0) != TileKind.Wall || tiles.GetTile(x, 39) != TileKind.Wall)
            {
                throw new InvalidOperationException("Top/bottom border contains a non-wall cell.");
            }
        }
        for (int y = 0; y < 40; y++)
        {
            if (tiles.GetTile(0, y) != TileKind.Wall || tiles.GetTile(49, y) != TileKind.Wall)
            {
                throw new InvalidOperationException("Left/right border contains a non-wall cell.");
            }
        }
    }

    [Fact]
    public void AllFloorCells_AreConnected()
    {
        var generator = new RoomsAndCorridorsLayoutGenerator();
        var tiles = generator.Generate(50, 40, seed: 7);

        var floorCells = FloorCells(tiles);
        if (floorCells.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one floor cell.");
        }

        // Flood fill from the first floor cell; every floor cell must be reachable.
        var visited = new HashSet<Point> { floorCells[0] };
        var queue = new Queue<Point>();
        queue.Enqueue(floorCells[0]);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in Neighbors(current))
            {
                if (tiles.Contains(neighbor) && tiles.IsWalkable(neighbor) && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (visited.Count != floorCells.Count)
        {
            throw new InvalidOperationException(
                $"Floor is disconnected: {visited.Count}/{floorCells.Count} cells reachable.");
        }
    }

    private static List<Point> FloorCells(TileMap tiles)
    {
        var cells = new List<Point>();
        for (int y = 0; y < tiles.GetHeight(); y++)
        {
            for (int x = 0; x < tiles.GetWidth(); x++)
            {
                if (tiles.IsWalkable(x, y))
                {
                    cells.Add(new Point(x, y));
                }
            }
        }
        return cells;
    }

    private static IEnumerable<Point> Neighbors(Point p)
    {
        yield return new Point(p.X - 1, p.Y);
        yield return new Point(p.X + 1, p.Y);
        yield return new Point(p.X, p.Y - 1);
        yield return new Point(p.X, p.Y + 1);
    }
}
