using MonoRogue.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class VisibilityTests
{
    [Fact]
    public void VisibilityMap_TracksFovAndExploredMemory()
    {
        var vis = new VisibilityMap(new TileMap(30, 20));

        // Before any FOV computation, nothing is seen or remembered.
        if (vis.IsInFov(15, 10) || vis.IsExplored(15, 10))
            throw new InvalidOperationException("Nothing should be visible before FOV is computed.");

        vis.Compute(new Point(15, 10), 5);

        // Origin is visible and remembered; a near cell is visible.
        if (!vis.IsInFov(15, 10) || !vis.IsExplored(15, 10))
            throw new InvalidOperationException("Origin should be visible and explored.");
        if (!vis.IsInFov(16, 10))
            throw new InvalidOperationException("A nearby cell should be in FOV.");

        // A far cell is neither visible nor remembered.
        if (vis.IsInFov(0, 0) || vis.IsExplored(0, 0))
            throw new InvalidOperationException("A distant cell should be hidden before being seen.");

        // Move the origin far away: previously seen cells become remembered (explored), not visible.
        vis.Compute(new Point(0, 0), 3);
        if (vis.IsInFov(15, 10))
            throw new InvalidOperationException("A distant remembered cell should be out of FOV.");
        if (!vis.IsExplored(15, 10))
            throw new InvalidOperationException("A previously seen cell should remain explored.");
    }

    [Fact]
    public void VisibilityMap_WallsBlockSight()
    {
        var tiles = new TileMap(11, 11);
        // A solid wall column at x=5 splits the map.
        for (int y = 0; y < 11; y++)
        {
            tiles.SetTile(5, y, TileKind.Wall);
        }

        var vis = new VisibilityMap(tiles);
        vis.Compute(new Point(2, 5), 20);

        if (!vis.IsInFov(3, 5))
            throw new InvalidOperationException("Cell on the origin's side of the wall should be visible.");
        if (vis.IsInFov(8, 5))
            throw new InvalidOperationException("Cell behind a wall should not be visible.");
    }

    [Fact]
    public void RenderSnapshot_HidesEntitiesOutsideFov()
    {
        using var session = new GameSession(40, 30);
        var player = session.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player.");

        var far = new Point(2, 2);                  // far corner, outside radius 10
        var near = new Point(player.X + 1, player.Y); // adjacent to the player

        session.GetWorld().Create(new Position(far), RenderGlyph.FromArgb('M', unchecked((int)0xFFFF0000), unchecked((int)0xFF000000)));
        session.GetWorld().Create(new Position(near), RenderGlyph.FromArgb('N', unchecked((int)0xFF00FF00), unchecked((int)0xFF000000)));

        var cells = session.GetRenderSnapshot();

        if (cells.Any(c => c.X == far.X && c.Y == far.Y && c.Glyph.Glyph == 'M'))
            throw new InvalidOperationException("An entity outside FOV should not appear in the snapshot.");
        if (!cells.Any(c => c.X == near.X && c.Y == near.Y && c.Glyph.Glyph == 'N'))
            throw new InvalidOperationException("An entity inside FOV should appear in the snapshot.");
    }

    [Fact]
    public void RenderSnapshot_TagsTerrainVisibility()
    {
        using var session = new GameSession(40, 30);
        var player = session.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player.");

        var cells = session.GetRenderSnapshot();

        // The player's own cell is visible.
        if (!cells.Any(c => c.X == player.X && c.Y == player.Y && c.Visibility == CellVisibility.Visible))
            throw new InvalidOperationException("Player cell should be tagged Visible.");

        // The far corner is hidden but still emitted (so the UI can render it as blank).
        if (!cells.Any(c => c.X == 0 && c.Y == 0 && c.Visibility == CellVisibility.Hidden))
            throw new InvalidOperationException("Far corner should be tagged Hidden.");
    }
}
