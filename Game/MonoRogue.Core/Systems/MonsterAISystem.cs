using Arch.Core;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core.Systems;

/// <summary>
/// Monster turn logic: plans and executes each monster's actions, finds monsters by
/// position, and removes dead monsters.
/// </summary>
public sealed class MonsterAISystem
{
    private readonly World _world;
    private readonly SpatialMap _spatial;
    private readonly CombatSystem _combat;
    private readonly EntityFactory _factory;
    private readonly QueryDescription _actorEntities;

    public MonsterAISystem(World world, SpatialMap spatial, CombatSystem combat, EntityFactory factory)
    {
        _world = world;
        _spatial = spatial;
        _combat = combat;
        _factory = factory;
        _actorEntities = new QueryDescription().WithAll<Position, ActorControlled, Energy, MonsterBehavior>();
    }

    public int ProcessActors(Point playerPosition, Entity playerEntity)
    {
        var actionsExecuted = 0;
        _world.Query(in _actorEntities, (Entity entity, ref Position monsterPosition, ref ActorControlled actor, ref Energy energy, ref MonsterBehavior behavior) =>
        {
            if (actor.Kind != ActorKind.Monster)
            {
                return;
            }

            var actionsForThisMonster = 0;
            while (actionsForThisMonster < GameConstants.MaxMonsterActionsPerTurn)
            {
                var action = PlanMonsterAction(monsterPosition.Value, playerPosition, behavior, energy);
                if (action.EnergyCost <= 0 || energy.Current < action.EnergyCost)
                {
                    break;
                }

                if (ExecuteMonsterAction(entity, ref monsterPosition, action, playerEntity))
                {
                    actionsExecuted++;
                }

                energy.Current -= action.EnergyCost;
                actionsForThisMonster++;
            }
        });

        return actionsExecuted;
    }

    public bool TryGetMonsterEntityAt(Point position, out Entity monsterEntity)
    {
        var result = default(Entity);
        var found = false;
        var query = new QueryDescription().WithAll<Position, ActorControlled>();
        _world.Query(in query, (Entity entity, ref Position pos, ref ActorControlled actor) =>
        {
            if (found || actor.Kind != ActorKind.Monster || pos.Value != position)
            {
                return;
            }

            result = entity;
            found = true;
        });

        monsterEntity = result;
        return found;
    }

    public int DestroyDeadMonsters()
    {
        var dead = new HashSet<Entity>();
        var query = new QueryDescription().WithAll<ActorControlled, Health>();
        _world.Query(in query, (Entity entity, ref ActorControlled actor, ref Health health) =>
        {
            if (actor.Kind == ActorKind.Monster && health.Current <= 0)
            {
                dead.Add(entity);
            }
        });

        var totalExperience = 0;
        if (dead.Count > 0)
        {
            var xpQuery = new QueryDescription().WithAll<ActorControlled, Experience>();
            _world.Query(in xpQuery, (Entity entity, ref Experience experience) =>
            {
                if (dead.Contains(entity))
                {
                    totalExperience += experience.Value;
                }
            });
        }

        foreach (var entity in dead)
        {
            _factory.Destroy(entity);
        }

        return totalExperience;
    }

    private MonsterActionPlan PlanMonsterAction(Point monsterPosition, Point playerPosition, MonsterBehavior behavior, Energy energy)
    {
        var delta = playerPosition - monsterPosition;
        var stepX = Math.Sign(delta.X);
        var stepY = Math.Sign(delta.Y);
        var distance = Math.Abs(delta.X) + Math.Abs(delta.Y);

        // Adjacent monsters strike directly instead of stepping into the player.
        if (distance == 1)
        {
            return new MonsterActionPlan(MonsterActionType.MeleeAttack, Point.None, Math.Max(1, energy.ActionCost));
        }

        if (behavior.Type == MonsterAIType.Breath && distance <= behavior.Range)
        {
            return new MonsterActionPlan(MonsterActionType.BreathAttack, Point.None, Math.Max(1, behavior.SpecialEnergyCost));
        }

        if (stepX != 0)
        {
            return new MonsterActionPlan(MonsterActionType.StepTowardPlayer, new Point(stepX, 0), Math.Max(1, energy.ActionCost));
        }

        if (stepY != 0)
        {
            return new MonsterActionPlan(MonsterActionType.StepTowardPlayer, new Point(0, stepY), Math.Max(1, energy.ActionCost));
        }

        return new MonsterActionPlan(MonsterActionType.Wait, Point.None, Math.Max(1, energy.ActionCost));
    }

    private bool ExecuteMonsterAction(Entity monsterEntity, ref Position monsterPosition, MonsterActionPlan action, Entity playerEntity)
    {
        switch (action.Type)
        {
            case MonsterActionType.StepTowardPlayer:
            {
                var candidate = monsterPosition.Value + action.Delta;
                if (CanMonsterOccupy(candidate, monsterPosition.Value))
                {
                    monsterPosition.Value = candidate;
                    return true;
                }

                return false;
            }
            case MonsterActionType.MeleeAttack:
            {
                var damage = _combat.GetAttackDamage(monsterEntity, GameConstants.DefaultMonsterAttack);
                return _combat.ApplyDamage(playerEntity, damage) > 0;
            }
            case MonsterActionType.BreathAttack:
            {
                var damage = _combat.GetAttackDamage(monsterEntity, GameConstants.DragonAttack);
                return _combat.ApplyDamage(playerEntity, damage) > 0;
            }
            case MonsterActionType.Wait:
            default:
                return false;
        }
    }

    private bool CanMonsterOccupy(Point destination, Point currentPosition)
    {
        return destination != currentPosition && _spatial.CanOccupy(destination);
    }
}
