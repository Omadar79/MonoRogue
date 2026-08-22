using Arch.Core;
using MonoRogue.Core.Systems;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Converts the ECS world and player inventory to/from plain DTOs (<see cref="MapData"/>)
/// for persistence. File I/O is handled separately by <see cref="MapPersistenceHelpers"/>.
/// </summary>
public sealed class MapSerializer
{
    private readonly World _world;
    private readonly EffectSystem _effects;
    private readonly Inventory _inventory;
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    private readonly QueryDescription _blockingEntities;
    private readonly QueryDescription _renderableEntities;
    private readonly QueryDescription _itemEntities;
    private readonly QueryDescription _effectEntities;
    private readonly QueryDescription _behaviorEntities;
    private readonly QueryDescription _healthEntities;
    private readonly QueryDescription _attackEntities;

    public MapSerializer(World world, EffectSystem effects, Inventory inventory, int mapWidth, int mapHeight)
    {
        _world = world;
        _effects = effects;
        _inventory = inventory;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;

        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
        _itemEntities = new QueryDescription().WithAll<Position, Item>();
        _effectEntities = new QueryDescription().WithAll<TimedEffect, EffectType, EffectTarget, EffectMagnitude>();
        _behaviorEntities = new QueryDescription().WithAll<MonsterBehavior>();
        _healthEntities = new QueryDescription().WithAll<Health>();
        _attackEntities = new QueryDescription().WithAll<Attack>();
    }

    public MapData Save()
    {
        var entities = new List<EntityDTO>();
        var effects = new List<EffectDTO>();
        var savedEntityIds = new Dictionary<Entity, int>();
        var nextSavedEntityId = 1;

        var blockingPositions = new HashSet<Point>();
        _world.Query(in _blockingEntities, (ref Position pos) => { blockingPositions.Add(pos.Value); });

        var playerPositions = new HashSet<Point>();
        var playerQuery = new QueryDescription().WithAll<Position, ActorControlled>();
        _world.Query(in playerQuery, (ref Position pos, ref ActorControlled actor) =>
            {
                if (actor.Kind == ActorKind.Player)
                {
                    playerPositions.Add(pos.Value);
                }
            });

        var itemInfo = new Dictionary<Entity, (ItemKind Kind, string Name, int Magnitude)>();
        _world.Query(in _itemEntities, (Entity entity, ref Item item) =>
            {
                itemInfo[entity] = (item.Kind, item.Name, item.Magnitude);
            });

        var behaviorInfo = new Dictionary<Entity, MonsterBehavior>();
        _world.Query(in _behaviorEntities, (Entity entity, ref MonsterBehavior behavior) =>
            {
                behaviorInfo[entity] = behavior;
            });

        var healthInfo = new Dictionary<Entity, Health>();
        _world.Query(in _healthEntities, (Entity entity, ref Health health) =>
            {
                healthInfo[entity] = health;
            });

        var attackInfo = new Dictionary<Entity, int>();
        _world.Query(in _attackEntities, (Entity entity, ref Attack attack) =>
            {
                attackInfo[entity] = attack.Damage;
            });

        _world.Query(in _renderableEntities, (Entity entity, ref Position pos, ref RenderGlyph glyph) =>
        {
            var glyphDto = new GlyphDTO(glyph.Value.Glyph, glyph.Value.ForegroundArgb, glyph.Value.BackgroundArgb);

            var isBlocked = blockingPositions.Contains(pos.Value);
            var isPlayer = playerPositions.Contains(pos.Value);
            var savedEntityId = nextSavedEntityId++;
            savedEntityIds[entity] = savedEntityId;

            ItemKind? itemKind = null;
            string? itemName = null;
            int itemMagnitude = 0;
            if (itemInfo.TryGetValue(entity, out var info))
            {
                itemKind = info.Kind;
                itemName = info.Name;
                itemMagnitude = info.Magnitude;
            }

            MonsterAIType? behaviorType = null;
            int behaviorRange = 0;
            int behaviorSpecialEnergyCost = 0;
            if (behaviorInfo.TryGetValue(entity, out var behavior))
            {
                behaviorType = behavior.Type;
                behaviorRange = behavior.Range;
                behaviorSpecialEnergyCost = behavior.SpecialEnergyCost;
            }

            int? healthCurrent = null;
            int? healthMax = null;
            if (healthInfo.TryGetValue(entity, out var health))
            {
                healthCurrent = health.Current;
                healthMax = health.Max;
            }

            int? attackDamage = null;
            if (attackInfo.TryGetValue(entity, out var attack))
            {
                attackDamage = attack;
            }

            entities.Add(new EntityDTO(pos.Value.X, pos.Value.Y, glyphDto, isBlocked, isPlayer, savedEntityId, itemKind, itemName, itemMagnitude, behaviorType, behaviorRange, behaviorSpecialEnergyCost, healthCurrent, healthMax, attackDamage));
        });

        _world.Query(in _effectEntities, (ref TimedEffect timed, ref EffectType type, ref EffectTarget target, ref EffectMagnitude magnitude) =>
        {
            if (!savedEntityIds.TryGetValue(target.Value, out var targetSavedEntityId))
            {
                return;
            }

            effects.Add(new EffectDTO(
                targetSavedEntityId,
                type.Value,
                timed.RemainingTime,
                timed.TickInterval,
                timed.TimeUntilNextTick,
                magnitude.Value));
        });

        var inventory = _inventory.Stacks.Select(s => new ItemStackDTO(s.Name, s.Kind, s.Count, s.Magnitude)).ToList();

        return new MapData(_mapWidth, _mapHeight, entities, effects, Inventory: inventory);
    }

    public void Load(MapData? mapData)
    {
        if (mapData == null)
        {
            return;
        }

        _world.Clear();

        var loadedEntityBySavedId = new Dictionary<int, Entity>();

        foreach (var e in mapData.Entities)
        {
            var pos = new Position(new Point(e.X, e.Y));
            var glyph = new RenderGlyph(new CoreGlyph(e.Glyph.Glyph, e.Glyph.ForegroundArgb, e.Glyph.BackgroundArgb));
            Entity createdEntity;

            if (e.IsPlayer)
            {
                createdEntity = _world.Create(
                    pos,
                    glyph,
                    new ActorControlled { Kind = ActorKind.Player },
                    new BlocksMovement(),
                    new Health { Current = e.Health ?? GameConstants.DefaultPlayerHealth, Max = e.MaxHealth ?? GameConstants.DefaultPlayerHealth },
                    new Attack { Damage = e.Attack ?? GameConstants.DefaultPlayerAttack },
                    new Energy { Current = GameConstants.DefaultActionCost, GainPerTurn = GameConstants.DefaultEnergyPerTurn, ActionCost = GameConstants.DefaultActionCost });
            }
            else if (e.BlocksMovement)
            {
                if (e.Glyph.Glyph is 'M' or 'g' or 'D')
                {
                    createdEntity = _world.Create(pos,
                        glyph,
                        new Health { Current = e.Health ?? GameConstants.DefaultMonsterHealth, Max = e.MaxHealth ?? GameConstants.DefaultMonsterHealth },
                        new Attack { Damage = e.Attack ?? (e.Glyph.Glyph == 'D' ? GameConstants.DragonAttack : GameConstants.DefaultMonsterAttack) },
                        new BlocksMovement(),
                        new ActorControlled { Kind = ActorKind.Monster },
                        ResolveBehavior(e),
                        new Energy { Current = 0, GainPerTurn = GameConstants.DefaultEnergyPerTurn, ActionCost = GameConstants.DefaultActionCost });
                }
                else
                {
                    createdEntity = _world.Create(pos, glyph, new BlocksMovement());
                }
            }
            else if (e.ItemName != null)
            {
                createdEntity = _world.Create(pos, glyph, new Item
                {
                    Kind = e.ItemKind ?? ItemKind.Gold,
                    Name = e.ItemName,
                    Magnitude = Math.Max(1, e.ItemMagnitude)
                });
            }
            else
            {
                createdEntity = _world.Create(pos, glyph);
            }

            if (e.SavedEntityId > 0)
            {
                loadedEntityBySavedId[e.SavedEntityId] = createdEntity;
            }
        }

        _inventory.Clear();
        if (mapData.Inventory != null)
        {
            foreach (var s in mapData.Inventory)
            {
                _inventory.AddStack(new ItemStack(s.Kind, s.Name, s.Count, s.Magnitude));
            }
        }

        if (mapData.Effects == null)
        {
            return;
        }

        foreach (var effect in mapData.Effects)
        {
            if (!loadedEntityBySavedId.TryGetValue(effect.TargetSavedEntityId, out var targetEntity))
            {
                continue;
            }

            _effects.CreateEffect(
                targetEntity,
                effect.Kind,
                effect.RemainingTime,
                effect.TickInterval,
                effect.Magnitude,
                effect.TimeUntilNextTick);
        }
    }

    // Restore a monster's behavior from the save DTO, falling back to glyph inference
    // for legacy saves that predate behavior persistence.
    private static MonsterBehavior ResolveBehavior(EntityDTO e)
    {
        if (e.Behavior is MonsterAIType type)
        {
            return new MonsterBehavior
            {
                Type = type,
                Range = Math.Max(1, e.BehaviorRange),
                SpecialEnergyCost = Math.Max(0, e.BehaviorSpecialEnergyCost)
            };
        }

        return MapGenerator.InferBehavior(e.Glyph.Glyph);
    }
}
