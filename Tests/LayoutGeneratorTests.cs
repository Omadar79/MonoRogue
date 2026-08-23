using MonoRogue.Core;

namespace MonoRogue.Tests;

public class LayoutGeneratorTests
{
    [Fact]
    public void RoomLayoutGenerator_CarvesBorderWallsAndOpenInterior()
    {
        var generator = new RoomLayoutGenerator();
        var tiles = generator.Generate(10, 8, seed: 123);

        if (tiles.GetWidth() != 10 || tiles.GetHeight() != 8)
        {
            throw new InvalidOperationException("Unexpected tile map dimensions.");
        }

        for (int x = 0; x < 10; x++)
        {
            if (tiles.GetTile(x, 0) != TileKind.Wall)
                throw new InvalidOperationException($"Top border ({x},0) is not a wall.");
            if (tiles.GetTile(x, 7) != TileKind.Wall)
                throw new InvalidOperationException($"Bottom border ({x},7) is not a wall.");
        }
        for (int y = 0; y < 8; y++)
        {
            if (tiles.GetTile(0, y) != TileKind.Wall)
                throw new InvalidOperationException($"Left border (0,{y}) is not a wall.");
            if (tiles.GetTile(9, y) != TileKind.Wall)
                throw new InvalidOperationException($"Right border (9,{y}) is not a wall.");
        }
        for (int y = 1; y < 7; y++)
        {
            for (int x = 1; x < 9; x++)
            {
                if (tiles.GetTile(x, y) != TileKind.Floor)
                    throw new InvalidOperationException($"Interior ({x},{y}) is not a floor.");
            }
        }
    }

    [Fact]
    public void GameSession_UsesInjectedLayoutGenerator()
    {
        var custom = new AllWallLayoutGenerator();
        using var session = new GameSession(8, 8, seed: 1, layoutGenerator: custom);

        if (session.GetTileMap().GetTile(4, 4) != TileKind.Wall)
        {
            throw new InvalidOperationException("Expected the injected generator's layout to be used.");
        }
    }

    private sealed class AllWallLayoutGenerator : IDungeonLayoutGenerator
    {
        public TileMap Generate(int width, int height, int seed)
        {
            var tiles = new TileMap(width, height);
            tiles.Fill(TileKind.Wall);
            return tiles;
        }
    }
}
