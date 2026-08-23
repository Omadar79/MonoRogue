using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Core.Systems;

/// <summary>
/// Health and damage logic: applying damage (with protection), healing, death checks,and reading attack/health values.
/// </summary>
public sealed class CombatSystem
{
    private readonly World _world;
    private readonly EffectSystem _effects;
    private readonly QueryDescription _healthEntities;
    private readonly QueryDescription _attackEntities;
    private readonly QueryDescription _renderableEntities;

    public CombatSystem(World world, EffectSystem effects)
    {
        _world = world;
        _effects = effects;
        _healthEntities = new QueryDescription().WithAll<Health>();
        _attackEntities = new QueryDescription().WithAll<Attack>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
    }

    public int ApplyDamage(Entity target, int rawDamage)
    {
        var damage = Math.Max(0, rawDamage - _effects.GetActiveProtection(target));
        if (damage <= 0)
        {
            return 0;
        }

        var applied = 0;
        _world.Query(in _healthEntities, (Entity entity, ref Health health) =>
        {
            if (entity != target)
            {
                return;
            }

            health.Current = Math.Max(0, health.Current - damage);
            applied = damage;
        });

        return applied;
    }

    public bool HealEntity(Entity entity, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        var healed = false;
        _world.Query(in _healthEntities, (Entity candidate, ref Health health) =>
        {
            if (candidate != entity)
            {
                return;
            }

            if (health.Current >= health.Max)
            {
                return;
            }

            health.Current = Math.Min(health.Max, health.Current + amount);
            healed = true;
        });

        return healed;
    }

    public bool IsEntityDead(Entity entity)
    {
        var dead = false;
        _world.Query(in _healthEntities, (Entity candidate, ref Health health) =>
        {
            if (candidate == entity && health.Current <= 0)
            {
                dead = true;
            }
        });

        return dead;
    }

    public int GetAttackDamage(Entity entity, int fallback)
    {
        var damage = fallback;
        _world.Query(in _attackEntities, (Entity candidate, ref Attack attack) =>
        {
            if (candidate == entity)
            {
                damage = attack.Damage;
            }
        });

        return damage;
    }

    public int GetHealthAt(Point position)
    {
        int? health = null;
        _world.Query(in _healthEntities, (Entity entity, ref Health value) =>
        {
            if (health != null)
            {
                return;
            }

            if (TryGetPosition(entity, out var entityPosition) && entityPosition == position)
            {
                health = value.Current;
            }
        });

        return health ?? 0;
    }

    public (int Current, int Max) GetHealth(Entity entity)
    {
        var result = (Current: 0, Max: 0);
        _world.Query(in _healthEntities, (Entity candidate, ref Health health) =>
        {
            if (candidate == entity)
            {
                result = (health.Current, health.Max);
            }
        });

        return result;
    }

    private bool TryGetPosition(Entity entity, out Point position)
    {
        var result = Point.None;
        var found = false;
        _world.Query(in _renderableEntities, (Entity candidate, ref Position value, ref RenderGlyph glyph) =>
        {
            if (found || candidate != entity)
            {
                return;
            }

            result = value.Value;
            found = true;
        });

        position = result;
        return found;
    }
}
