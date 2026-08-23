using Arch.Core;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Single owner of all world structural mutations (entity creation, destruction, and clearing).
/// Both <see cref="MapGenerator"/> (template-driven) and <see cref="MapSerializer"/>
/// (DTO-driven) construct entities through here so the component sets that define
/// "what a monster/player/item is" live in exactly one place.
/// </summary>
public sealed class EntityFactory
{
    private readonly World _world;

    public EntityFactory(World world)
    {
        _world = world;
    }

    /// <summary>
    /// Removes all entities from the world (used before loading a save).
    /// </summary>
    public void ClearWorld() => _world.Clear();

    /// <summary>
    /// Removes a single entity from the world.
    /// </summary>
    public void Destroy(Entity entity) => _world.Destroy(entity);

    public Entity CreatePlayer(Point position, CoreGlyph glyph, Health health, int attack) =>
        _world.Create(
            new Position(position),
            new RenderGlyph(glyph),
            new ActorControlled { Kind = ActorKind.Player },
            new BlocksMovement(),
            new Health { Current = health.Current, Max = health.Max },
            new Attack { Damage = attack },
            new Energy
                {
                    Current = GameConstants.DefaultActionCost,
                    GainPerTurn = GameConstants.DefaultEnergyPerTurn,
                    ActionCost = GameConstants.DefaultActionCost
                }
            );

    public Entity CreateMonster(Point position, CoreGlyph glyph, Health health, int attack, MonsterBehavior behavior, int gainPerTurn, int actionCost, int experience) =>
        _world.Create(
            new Position(position),
            new RenderGlyph(glyph),
            new Health { Current = health.Current, Max = health.Max },
            new Attack { Damage = attack },
            new BlocksMovement(),
            new ActorControlled { Kind = ActorKind.Monster },
            behavior,
            new Experience { Value = Math.Max(0, experience) },
            new Energy
            {
                Current = 0,
                GainPerTurn = Math.Max(1, gainPerTurn),
                ActionCost = Math.Max(1, actionCost)
            });

    public Entity CreateItem(Point position, CoreGlyph glyph, ItemKind kind, string name, int magnitude) =>
        _world.Create(
            new Position(position),
            new RenderGlyph(glyph),
            new Item { Kind = kind, Name = name, Magnitude = Math.Max(1, magnitude) });

    /// <summary>A tile/obstacle that blocks movement but is not an actor.</summary>
    public Entity CreateBlocker(Point position, CoreGlyph glyph)
    {
        return _world.Create(new Position(position), new RenderGlyph(glyph), new BlocksMovement());
    }
        

    /// <summary>A purely cosmetic entity (glyph only, no behavior or blocking).</summary>
    public Entity CreateDecoration(Point position, CoreGlyph glyph)
    {
        return _world.Create(new Position(position), new RenderGlyph(glyph));
    }

    /// <summary>Creates a timed status effect attached to a target entity.</summary>
    public Entity CreateEffect(Entity target, EffectKind kind, TimedEffect timed, int magnitude)
    {
        return _world.Create(
            timed,
            new EffectType { Value = kind },
            new EffectTarget { Value = target },
            new EffectMagnitude { Value = Math.Max(0, magnitude) });
    }

    // Infer monster AI from a legacy glyph when no JSON definition provides behavior.
    // Used only for the no-content fallback and for legacy saved maps.
    internal static MonsterBehavior InferBehavior(char glyph)
    {
        return glyph == 'D'
            ? new MonsterBehavior { Type = MonsterAIType.Breath, Range = 3, SpecialEnergyCost = 300 }
            : new MonsterBehavior { Type = MonsterAIType.Melee, Range = 1, SpecialEnergyCost = 0 };
    }
}
