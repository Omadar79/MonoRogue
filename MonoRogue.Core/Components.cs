using SadConsole;
using SadRogue.Primitives;

namespace MonoRogue.Core;

public struct Position
{
    public Point Value;

    public Position(Point value)
    {
        Value = value;
    }
}

public struct RenderGlyph
{
    public ColoredGlyph Value;

    public RenderGlyph(ColoredGlyph value)
    {
        Value = value;
    }
}

public readonly struct PlayerControlled
{
}

public readonly struct BlocksMovement
{
}