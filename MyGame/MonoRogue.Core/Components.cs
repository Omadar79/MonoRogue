using Arch.Core;
using MonoRogue.Data;
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

// A single renderable cell, used to hand glyph data to the UI without exposing
// save/serialization concerns. Kept UI-agnostic (raw ARGB ints, no SadConsole types).
public readonly record struct RenderCell(int X, int Y, CoreGlyph Glyph);

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

// Base damage dealt when this entity attacks another.
public struct Attack
{
    public int Damage;
}

// Describes how a monster plans its actions. Populated from MonsterDefinition so
// content can drive AI instead of hardcoding behavior by glyph character.
public struct MonsterBehavior
{
    public MonsterAIType Type;
    public int Range;
    public int SpecialEnergyCost;
}

// Marks an entity as a pickup item lying on the map.
public struct Item
{
    public ItemKind Kind;
    public string Name;
    public int Magnitude;
}

// One stack of items held in the player's inventory.
public readonly record struct ItemStack(ItemKind Kind, string Name, int Count, int Magnitude);

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
    MeleeAttack,
    BreathAttack
}

public readonly record struct MonsterActionPlan(MonsterActionType Type, SadRogue.Primitives.Point Delta, int EnergyCost);

public readonly record struct TurnResult(
    bool PlayerMoved,
    int MonsterActionsExecuted,
    int EffectTicksProcessed = 0,
    int EffectsExpired = 0,
    bool PlayerAttacked = false,
    int DamageDealt = 0,
    bool MonsterKilled = false,
    bool PlayerDied = false,
    bool ItemPickedUp = false,
    string? ItemPickedUpName = null,
    bool PotionUsed = false,
    int HealAmount = 0);

public readonly record struct EffectTickResult(int TicksProcessed, int EffectsExpired);

public readonly struct BlocksMovement
{
}