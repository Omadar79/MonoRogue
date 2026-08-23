using RogueSharp;
using SadRogue.Primitives;
using Point = SadRogue.Primitives.Point;

namespace MonoRogue.Core;

/// <summary>
/// Field-of-view and exploration memory, backed by RogueSharp's line-of-sight algorithm.
/// Terrain is synced from the static <see cref="TileMap"/> (walls block sight and movement;
/// floors are transparent). The set of currently visible cells comes from
/// <see cref="Map.ComputeFov"/> each frame, while exploration memory (cells ever seen) is
/// accumulated here so previously seen areas stay "remembered" after they leave sight.
/// </summary>
public sealed class VisibilityMap
{
    private readonly Map _map;
    private readonly HashSet<(int X, int Y)> _explored = new();
    private HashSet<(int X, int Y)> _inFov = new();

    public VisibilityMap(TileMap tiles)
    {
        _map = new Map(tiles.GetWidth(), tiles.GetHeight());
        Sync(tiles);
    }

    /// <summary>Rebuilds the RogueSharp map from the current terrain grid, clearing memory.</summary>
    public void Sync(TileMap tiles)
    {
        _map.Initialize(tiles.GetWidth(), tiles.GetHeight());
        for (int y = 0; y < tiles.GetHeight(); y++)
        {
            for (int x = 0; x < tiles.GetWidth(); x++)
            {
                var wall = tiles.GetTile(x, y) == TileKind.Wall;
                _map.SetCellProperties(x, y, isTransparent: !wall, isWalkable: !wall);
            }
        }

        _inFov.Clear();
        _explored.Clear();
    }

    /// <summary>Recomputes field of view from the given origin cell, lighting walls.</summary>
    public void Compute(Point origin, int radius)
    {
        var visible = new HashSet<(int X, int Y)>();
        foreach (var cell in _map.ComputeFov(origin.X, origin.Y, radius, lightWalls: true))
        {
            visible.Add((cell.X, cell.Y));
        }

        _inFov = visible;
        _explored.UnionWith(visible);
    }

    public bool IsInFov(int x, int y) => _inFov.Contains((x, y));

    public bool IsExplored(int x, int y) => _explored.Contains((x, y));

    public CellVisibility GetVisibility(int x, int y)
    {
        if (IsInFov(x, y))
        {
            return CellVisibility.Visible;
        }
        return IsExplored(x, y) ? CellVisibility.Explored : CellVisibility.Hidden;
    }
}
