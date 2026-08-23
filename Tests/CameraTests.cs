using MonoRogue.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class CameraTests
{
    [Fact]
    public void Camera_CentersOnFocusWhenWorldLargerThanViewport()
    {
        // 200x100 world, 100x30 viewport, focus at the world center.
        var topLeft = Camera.ComputeTopLeft(100, 30, 200, 100, new Point(100, 50));

        if (topLeft.X != 50 || topLeft.Y != 35)
        {
            throw new InvalidOperationException($"Expected (50,35) but got ({topLeft.X},{topLeft.Y}).");
        }
    }

    [Fact]
    public void Camera_ClampsToTopLeftWhenFocusNearOrigin()
    {
        var topLeft = Camera.ComputeTopLeft(100, 30, 200, 100, new Point(0, 0));

        if (topLeft.X != 0 || topLeft.Y != 0)
        {
            throw new InvalidOperationException($"Expected (0,0) but got ({topLeft.X},{topLeft.Y}).");
        }
    }

    [Fact]
    public void Camera_ClampsToBottomRightWhenFocusNearFarCorner()
    {
        var topLeft = Camera.ComputeTopLeft(100, 30, 200, 100, new Point(199, 99));

        if (topLeft.X != 100 || topLeft.Y != 70)
        {
            throw new InvalidOperationException($"Expected (100,70) but got ({topLeft.X},{topLeft.Y}).");
        }
    }

    [Fact]
    public void Camera_IsZeroWhenWorldFitsInViewport()
    {
        var topLeft = Camera.ComputeTopLeft(100, 30, 80, 20, new Point(40, 10));

        if (topLeft.X != 0 || topLeft.Y != 0)
        {
            throw new InvalidOperationException($"Expected (0,0) but got ({topLeft.X},{topLeft.Y}).");
        }
    }
}
