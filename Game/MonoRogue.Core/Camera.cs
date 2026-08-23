using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Computes the visible viewport (its top-left world offset) into a world map. Kept UI-agnostic so the math can be unit
/// tested without the presentation stack.
/// </summary>
public static class Camera
{
    // Computes the top-left world coordinate of a viewport of the given size, centered on <paramref name="focus"/>
    // and clamped so the view never leaves the world bounds. When the world is smaller than the viewport the offset is (0, 0).
    public static Point ComputeTopLeft(int viewWidth, int viewHeight, int worldWidth, int worldHeight, Point focus)
    {
        var x = focus.X - viewWidth / 2;
        var y = focus.Y - viewHeight / 2;

        x = Math.Clamp(x, 0, Math.Max(0, worldWidth - viewWidth));
        y = Math.Clamp(y, 0, Math.Max(0, worldHeight - viewHeight));

        return new Point(x, y);
    }
}
