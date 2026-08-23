using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Static terrain a map cell can hold. Separate from entities: walls and floors are part of the dungeon layout, 
/// not ECS entities.
/// </summary>
public enum TileKind
{
    Floor,
    Wall
}

/// <summary>
/// A fixed-size 2D grid of <see cref="TileKind"/> cells representing the dungeon layout.  Owned by <see cref="SpatialMap"/>
/// so movement blocking and rendering can consult terrain without touching the ECS world.
/// </summary>
public sealed class TileMap
{
    private readonly TileKind[,] _tiles;

    public TileMap(int width, int height)
    {
        _tiles = new TileKind[width, height]; // all cells default to Floor
    }

    public int GetWidth() => _tiles.GetLength(0);
    public int GetHeight() => _tiles.GetLength(1);

    public TileKind GetTile(int x, int y) => _tiles[x, y];
    public TileKind GetTile(Point position) => _tiles[position.X, position.Y];

    public void SetTile(int x, int y, TileKind kind) => _tiles[x, y] = kind;
    public void SetTile(Point position, TileKind kind) => _tiles[position.X, position.Y] = kind;

    public bool IsWalkable(int x, int y) => _tiles[x, y] == TileKind.Floor;
    public bool IsWalkable(Point position) => IsWalkable(position.X, position.Y);

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < GetWidth() && y < GetHeight();
    public bool Contains(Point position) => Contains(position.X, position.Y);
}
