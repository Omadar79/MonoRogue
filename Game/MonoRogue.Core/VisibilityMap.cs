using RogueSharp;
using SadRogue.Primitives;
using Point = SadRogue.Primitives.Point;

namespace MonoRogue.Core;

/// <summary>
/// Field-of-view and exploration memory, backed by RogueSharp's line-of-sight algorithm. Terrain is synced from the
/// static <see cref="TileMap"/> (walls block sight and movement; floors are transparent). The set of currently visible
/// cells comes from <see cref="Map.ComputeFov"/> each frame, while exploration memory (cells ever seen) is
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

    //Rebuilds the RogueSharp map from the current terrain grid, clearing sight and exploration
    //memory. Restore explored memory afterwards with <see cref="RestoreExplored"/> when the
    //level being loaded was visited before.
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

    //Recomputes field of view from the given origin cell, lighting walls.
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

    /// <summary>All cells ever seen on the current level (explored memory).</summary>
    public List<VisibilityCellDTO> CaptureExplored()
    {
        var cells = new List<VisibilityCellDTO>(_explored.Count);
        foreach (var (x, y) in _explored)
        {
            cells.Add(new VisibilityCellDTO(x, y));
        }
        return cells;
    }

    /// <summary>Restores explored memory from a prior capture. Call after <see cref="Sync"/> for the same level.</summary>
    public void RestoreExplored(List<VisibilityCellDTO> cells)
    {
        foreach (var cell in cells)
        {
            if (cell.X >= 0 && cell.X < _map.Width && cell.Y >= 0 && cell.Y < _map.Height)
            {
                _explored.Add((cell.X, cell.Y));
            }
        }
    }

    /// <summary>Clears exploration memory (a fresh, never-visited level).</summary>
    public void ClearExplored()
    {
        _explored.Clear();
    }

    public CellVisibility GetVisibility(int x, int y)
    {
        if (IsInFov(x, y))
        {
            return CellVisibility.Visible;
        }
        return IsExplored(x, y) ? CellVisibility.Explored : CellVisibility.Hidden;
    }
}
