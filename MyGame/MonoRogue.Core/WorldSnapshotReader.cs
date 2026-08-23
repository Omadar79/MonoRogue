using Arch.Core;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// A point-in-time read of every entity in the world, captured once so the
/// serializer can map it to DTOs without holding a <see cref="World"/> reference.
/// </summary>
public sealed record WorldSnapshot(
    List<SnapshotRenderable> Renderables,
    HashSet<Point> BlockingPositions,
    HashSet<Point> PlayerPositions,
    Dictionary<Entity, SnapshotItem> Items,
    Dictionary<Entity, MonsterBehavior> Behaviors,
    Dictionary<Entity, Health> Health,
    Dictionary<Entity, int> Attack,
    Dictionary<Entity, int> Experience,
    List<SnapshotEffect> Effects);

public readonly record struct SnapshotRenderable(Entity Entity, Point Position, CoreGlyph Glyph);

public readonly record struct SnapshotItem(ItemKind Kind, string Name, int Magnitude);

public readonly record struct SnapshotEffect(Entity Target, EffectKind Kind, TimedEffect Timed, int Magnitude);

/// <summary>
/// Owns all the read queries needed to snapshot the world for persistence. This keeps
/// raw query descriptions in one place so <see cref="MapSerializer"/> composes a
/// <see cref="WorldSnapshot"/> instead of reaching into the world itself.
/// </summary>
public sealed class WorldSnapshotReader
{
    private readonly World _world;

    private readonly QueryDescription _blockingEntities;
    private readonly QueryDescription _renderableEntities;
    private readonly QueryDescription _itemEntities;
    private readonly QueryDescription _effectEntities;
    private readonly QueryDescription _behaviorEntities;
    private readonly QueryDescription _healthEntities;
    private readonly QueryDescription _attackEntities;
    private readonly QueryDescription _experienceEntities;
    private readonly QueryDescription _actorEntities;

    public WorldSnapshotReader(World world)
    {
        _world = world;
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
        _itemEntities = new QueryDescription().WithAll<Position, Item>();
        _effectEntities = new QueryDescription().WithAll<TimedEffect, EffectType, EffectTarget, EffectMagnitude>();
        _behaviorEntities = new QueryDescription().WithAll<MonsterBehavior>();
        _healthEntities = new QueryDescription().WithAll<Health>();
        _attackEntities = new QueryDescription().WithAll<Attack>();
        _experienceEntities = new QueryDescription().WithAll<Experience>();
        _actorEntities = new QueryDescription().WithAll<Position, ActorControlled>();
    }

    public WorldSnapshot Capture()
    {
        var blockingPositions = new HashSet<Point>();
        _world.Query(in _blockingEntities, (ref Position pos) => { blockingPositions.Add(pos.Value); });

        var playerPositions = new HashSet<Point>();
        _world.Query(in _actorEntities, (ref Position pos, ref ActorControlled actor) =>
        {
            if (actor.Kind == ActorKind.Player)
            {
                playerPositions.Add(pos.Value);
            }
        });

        var items = new Dictionary<Entity, SnapshotItem>();
        _world.Query(in _itemEntities, (Entity entity, ref Item item) =>
        {
            items[entity] = new SnapshotItem(item.Kind, item.Name, item.Magnitude);
        });

        var behaviors = new Dictionary<Entity, MonsterBehavior>();
        _world.Query(in _behaviorEntities, (Entity entity, ref MonsterBehavior behavior) =>
        {
            behaviors[entity] = behavior;
        });

        var health = new Dictionary<Entity, Health>();
        _world.Query(in _healthEntities, (Entity entity, ref Health value) =>
        {
            health[entity] = value;
        });

        var attack = new Dictionary<Entity, int>();
        _world.Query(in _attackEntities, (Entity entity, ref Attack value) =>
        {
            attack[entity] = value.Damage;
        });

        var experience = new Dictionary<Entity, int>();
        _world.Query(in _experienceEntities, (Entity entity, ref Experience value) =>
        {
            experience[entity] = value.Value;
        });

        var renderables = new List<SnapshotRenderable>();
        _world.Query(in _renderableEntities, (Entity entity, ref Position pos, ref RenderGlyph glyph) =>
        {
            renderables.Add(new SnapshotRenderable(entity, pos.Value, glyph.Value));
        });

        var effects = new List<SnapshotEffect>();
        _world.Query(in _effectEntities, (ref TimedEffect timed, ref EffectType type, ref EffectTarget target, ref EffectMagnitude magnitude) =>
        {
            effects.Add(new SnapshotEffect(target.Value, type.Value, timed, magnitude.Value));
        });

        return new WorldSnapshot(renderables, blockingPositions, playerPositions, items, behaviors, health, attack, experience, effects);
    }
}
