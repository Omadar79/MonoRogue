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
    private readonly PathfindingService _pathfinding;
    private readonly QueryDescription _actorEntities;

    public MonsterAISystem(World world, SpatialMap spatial, CombatSystem combat, EntityFactory factory, PathfindingService pathfinding)
    {
        _world = world;
        _spatial = spatial;
        _combat = combat;
        _factory = factory;
        _pathfinding = pathfinding;
        _actorEntities = new QueryDescription().WithAll<Position, ActorControlled, Energy, MonsterBehavior, MonsterMemory>();
    }

    public int ProcessActors(Point playerPosition, Entity playerEntity)
    {
        var actionsExecuted = 0;
        _world.Query(in _actorEntities, (Entity entity, ref Position monsterPosition, ref ActorControlled actor, ref Energy energy, ref MonsterBehavior behavior, ref MonsterMemory memory) =>
        {
            if (actor.Kind != ActorKind.Monster)
            {
                return;
            }

            var actionsForThisMonster = 0;
            while (actionsForThisMonster < GameConstants.MaxMonsterActionsPerTurn)
            {
                // Sight is recomputed each action so a monster that moves into or out of the
                // player's view reacts correctly within the same turn.
                var seesPlayer = _spatial.HasLineOfSight(monsterPosition.Value, playerPosition);
                var action = PlanMonsterAction(monsterPosition.Value, playerPosition, behavior, energy, ref memory, seesPlayer);
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

    private MonsterActionPlan PlanMonsterAction(Point monsterPosition, Point playerPosition, MonsterBehavior behavior, Energy energy, ref MonsterMemory memory, bool seesPlayer)
    {
        var delta = playerPosition - monsterPosition;
        var distance = Math.Abs(delta.X) + Math.Abs(delta.Y);

        // An adjacent monster strikes directly; adjacency implies the player is visible.
        if (distance == 1)
        {
            return new MonsterActionPlan(MonsterActionType.MeleeAttack, Point.None, Math.Max(1, energy.ActionCost));
        }

        // Seeing the player refreshes the monster's memory of where the player was.
        if (seesPlayer)
        {
            memory.HasSeenPlayer = true;
            memory.LastSeenPosition = playerPosition;
        }

        if (behavior.Type == MonsterAIType.Breath && seesPlayer && distance <= behavior.Range)
        {
            return new MonsterActionPlan(MonsterActionType.BreathAttack, Point.None, Math.Max(1, behavior.SpecialEnergyCost));
        }

        // Chase the live player while visible; otherwise move toward the last position the
        // player was seen at. A monster that has never seen the player simply waits.
        Point chaseTarget;
        if (seesPlayer)
        {
            chaseTarget = playerPosition;
        }
        else if (memory.HasSeenPlayer)
        {
            chaseTarget = memory.LastSeenPosition;
        }
        else
        {
            return new MonsterActionPlan(MonsterActionType.Wait, Point.None, Math.Max(1, energy.ActionCost));
        }

        // Navigate around walls and other monsters via A* pathfinding. When no path exists
        // (the target is unreachable), the monster waits instead of bumping into obstacles.
        if (_pathfinding.GetNextStep(monsterPosition, chaseTarget) is Point nextStep)
        {
            return new MonsterActionPlan(MonsterActionType.StepTowardPlayer, nextStep - monsterPosition, Math.Max(1, energy.ActionCost));
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
