using Arch.Core;
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
    public CoreGlyph Value;

    public RenderGlyph(CoreGlyph value)
    {
        Value = value;
    }

    public static RenderGlyph FromArgb(char glyph, int foregroundArgb, int backgroundArgb)
    {
        return new RenderGlyph(new CoreGlyph(glyph, foregroundArgb, backgroundArgb));
    }
}

public readonly record struct CoreGlyph(char Glyph, int ForegroundArgb, int BackgroundArgb);

// ActorKind and ActorControlled unify player/monster markers so systems can
// operate on actors uniformly while still distinguishing player vs monster.
public enum ActorKind
{
    Player,
    Monster
}

public struct ActorControlled
{
    public ActorKind Kind;
}

public struct Energy
{
    // Current stored energy; any actor acts while Current >= ActionCost.
    public int Current;

    // Energy gained each turn (100 means one action per turn for a 100-cost actor).
    public int GainPerTurn;

    // Cost to perform one action.
    public int ActionCost;
}

public struct Health
{
    public int Current;
    public int Max;
}

public enum EffectKind
{
    Light,
    Protection,
    Poison
}

public struct TimedEffect
{
    // Remaining effect lifetime in turn-time units.
    public int RemainingTime;

    // Pulse cadence in turn-time units; 0 means passive/no pulse.
    public int TickInterval;

    // Time until the next pulse.
    public int TimeUntilNextTick;
}

public struct EffectTarget
{
    public Entity Value;
}

public struct EffectMagnitude
{
    public int Value;
}

public struct EffectType
{
    public EffectKind Value;
}

public enum MonsterActionType
{
    Wait,
    StepTowardPlayer,
    BreathAttack
}

public readonly record struct MonsterActionPlan(MonsterActionType Type, SadRogue.Primitives.Point Delta, int EnergyCost);

public readonly record struct TurnResult(
    bool PlayerMoved,
    int MonsterActionsExecuted,
    int EffectTicksProcessed = 0,
    int EffectsExpired = 0);

public readonly record struct EffectTickResult(int TicksProcessed, int EffectsExpired);

public readonly struct BlocksMovement
{
}