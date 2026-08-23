namespace MonoRogue.Core;

/// <summary>
/// Trivial layout generator: a single open room surrounded by border walls. This mirrors the original hand-carved 
/// layout and is the baseline implementation behind the <see cref="IDungeonLayoutGenerator"/> seam.
/// </summary>
public sealed class RoomLayoutGenerator : IDungeonLayoutGenerator
{
    public TileMap Generate(int width, int height, int seed)
    {
        // The seed is unused here: a fixed open room has no randomness. Later generators
        // (rooms-and-corridors, caves, BSP) consume it for reproducible carving.
        var tiles = new TileMap(width, height);
        for (int x = 0; x < width; x++)
        {
            tiles.SetTile(x, 0, TileKind.Wall);
            tiles.SetTile(x, height - 1, TileKind.Wall);
        }
        for (int y = 0; y < height; y++)
        {
            tiles.SetTile(0, y, TileKind.Wall);
            tiles.SetTile(width - 1, y, TileKind.Wall);
        }
        return tiles;
    }
}
