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

public readonly struct MonsterControlled
{
}

public struct Energy
{
    // Current stored energy; monsters act while Current >= ActionCost.
    public int Current;

    // Energy gained each player turn (100 means one action per player action).
    public int GainPerTurn;

    // Cost to perform one action.
    public int ActionCost;
}

public enum MonsterActionType
{
    Wait,
    StepTowardPlayer,
    BreathAttack
}

public readonly record struct MonsterActionPlan(MonsterActionType Type, SadRogue.Primitives.Point Delta, int EnergyCost);

public readonly record struct TurnResult(bool PlayerMoved, int MonsterActionsExecuted);

public readonly struct BlocksMovement
{
}