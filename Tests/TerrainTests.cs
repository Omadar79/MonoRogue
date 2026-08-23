using MonoRogue.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class TerrainTests
{
    [Fact]
    public void BorderWalls_AreWallsAndCenterIsFloor()
    {
        using var map = new GameSession(10, 10);
        var tiles = map.GetTileMap();

        if (tiles.GetTile(0, 0) != TileKind.Wall) throw new InvalidOperationException("Expected top-left corner to be a wall.");
        if (tiles.GetTile(9, 9) != TileKind.Wall) throw new InvalidOperationException("Expected bottom-right corner to be a wall.");
        if (tiles.GetTile(5, 5) != TileKind.Floor) throw new InvalidOperationException("Expected center to be floor.");
    }

    [Fact]
    public void Walls_BlockMovement()
    {
        using var map = new GameSession(10, 10);

        if (!map.IsBlocked(new Point(0, 0))) throw new InvalidOperationException("Expected a wall cell to be blocked.");
        if (map.GetTileMap().IsWalkable(new Point(0, 0))) throw new InvalidOperationException("Expected a wall tile to be non-walkable.");
        if (!map.GetTileMap().IsWalkable(new Point(5, 5))) throw new InvalidOperationException("Expected a floor tile to be walkable.");
    }

    [Fact]
    public void RenderSnapshot_ContainsTerrainAndEntities()
    {
        using var map = new GameSession(10, 10);
        var cells = map.GetRenderSnapshot();

        if (!cells.Any(c => c.X == 0 && c.Y == 0 && c.Glyph.Glyph == '#')) throw new InvalidOperationException("Expected a wall glyph at the border.");
        if (!cells.Any(c => c.X == 5 && c.Y == 5 && c.Glyph.Glyph == '.')) throw new InvalidOperationException("Expected a floor glyph at the center.");
        if (!cells.Any(c => c.Glyph.Glyph == '@')) throw new InvalidOperationException("Expected the player glyph in the snapshot.");
    }
}
