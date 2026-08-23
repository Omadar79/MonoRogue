using Arch.Core;
using MonoRogue.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class MapGeneratorTests
{
    [Fact]
    public void AllSpawnedEntities_AreOnWalkableFloor()
    {
        using var map = new GameSession(20, 20);
        var tiles = map.GetTileMap();

        var positions = new List<Point>();
        var query = new QueryDescription().WithAll<Position>();
        map.GetWorld().Query(in query, (ref Position pos) => positions.Add(pos.Value));

        if (positions.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one positioned entity.");
        }

        foreach (var position in positions)
        {
            if (!tiles.IsWalkable(position))
            {
                throw new InvalidOperationException($"Entity spawned on a non-floor cell ({position.X},{position.Y}).");
            }
        }
    }

    [Fact]
    public void GetRandomWalkableCell_NeverReturnsWall()
    {
        using var world = World.Create();
        var spatial = new SpatialMap(world, 10, 10);

        // Carve a border so there are both walls and floors.
        for (int x = 0; x < 10; x++)
        {
            spatial.GetTileMap().SetTile(x, 0, TileKind.Wall);
            spatial.GetTileMap().SetTile(x, 9, TileKind.Wall);
        }
        for (int y = 0; y < 10; y++)
        {
            spatial.GetTileMap().SetTile(0, y, TileKind.Wall);
            spatial.GetTileMap().SetTile(9, y, TileKind.Wall);
        }

        var rng = new Random(42);
        for (int i = 0; i < 200; i++)
        {
            var cell = spatial.GetRandomWalkableCell(rng) ?? throw new InvalidOperationException("Expected a walkable cell.");
            if (!spatial.GetTileMap().IsWalkable(cell))
            {
                throw new InvalidOperationException($"GetRandomWalkableCell returned a wall ({cell.X},{cell.Y}).");
            }
        }
    }

    [Fact]
    public void GetRandomOpenCell_AvoidsBlockingEntities()
    {
        using var world = World.Create();
        var spatial = new SpatialMap(world, 5, 5); // all floor

        // Block the center cell with an entity.
        world.Create(new Position(new Point(2, 2)), new BlocksMovement());

        var rng = new Random(7);
        for (int i = 0; i < 200; i++)
        {
            var cell = spatial.GetRandomOpenCell(rng) ?? throw new InvalidOperationException("Expected an open cell.");
            if (!spatial.CanOccupy(cell))
            {
                throw new InvalidOperationException($"GetRandomOpenCell returned a blocked cell ({cell.X},{cell.Y}).");
            }
        }
    }

    [Fact]
    public void SameSeed_ProducesIdenticalMap()
    {
        using var map1 = new GameSession(20, 20, 42);
        using var map2 = new GameSession(20, 20, 42);

        var cells1 = CanonicalCellKeys(map1);
        var cells2 = CanonicalCellKeys(map2);

        if (!cells1.SequenceEqual(cells2))
        {
            throw new InvalidOperationException("Expected identical maps for the same seed.");
        }
    }

    private static List<string> CanonicalCellKeys(GameSession session)
    {
        return session.GetRenderSnapshot()
            .Select(c => $"{c.X},{c.Y},{c.Glyph.Glyph}")
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
    }
}
