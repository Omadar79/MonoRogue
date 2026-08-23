namespace MonoRogue.Core;

/// <summary>
/// Produces a static terrain layout (a <see cref="TileMap"/>) for a dungeon of the given
/// size. Implementations are deterministic for a given seed, enabling reproducible
/// procedural generation. This is the seam where rooms-and-corridors, cellular-automata
/// caves, BSP, and other layout algorithms plug in.
/// </summary>
public interface IDungeonLayoutGenerator
{
    /// <summary>Generates a fully-carved terrain grid for a map of the given dimensions.</summary>
    TileMap Generate(int width, int height, int seed);
}
