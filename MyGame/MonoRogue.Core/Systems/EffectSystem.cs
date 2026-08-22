using Arch.Core;

namespace MonoRogue.Core.Systems;

/// <summary>
/// Owns timed effects: creation, ticking, expiry, and protection lookups.
/// Poison damage is applied via an injected <c>Func&lt;Entity,int,int&gt;</c> so this
/// system does not depend on <see cref="CombatSystem"/> (avoiding a dependency cycle).
/// </summary>
public sealed class EffectSystem
{
    private readonly World _world;
    private readonly QueryDescription _effectEntities;

    public EffectSystem(World world)
    {
        _world = world;
        _effectEntities = new QueryDescription().WithAll<TimedEffect, EffectType, EffectTarget, EffectMagnitude>();
    }

    public EffectTickResult ProcessEffects(int elapsedTime, Func<Entity, int, int> applyDamage)
    {
        if (elapsedTime <= 0)
        {
            return new EffectTickResult(0, 0);
        }

        var ticksProcessed = 0;
        var expired = new List<Entity>();

        _world.Query(in _effectEntities, (Entity effectEntity, ref TimedEffect timed, ref EffectType effectType, ref EffectTarget target, ref EffectMagnitude magnitude) =>
        {
            timed.RemainingTime -= elapsedTime;

            if (timed.TickInterval > 0)
            {
                timed.TimeUntilNextTick -= elapsedTime;
                while (timed.RemainingTime > 0 && timed.TimeUntilNextTick <= 0)
                {
                    if (ApplyEffectTick(effectType.Value, target.Value, magnitude.Value, applyDamage))
                    {
                        ticksProcessed++;
                    }

                    timed.TimeUntilNextTick += timed.TickInterval;
                }
            }

            if (timed.RemainingTime <= 0)
            {
                expired.Add(effectEntity);
            }
        });

        foreach (var effectEntity in expired)
        {
            _world.Destroy(effectEntity);
        }

        return new EffectTickResult(ticksProcessed, expired.Count);
    }

    public int GetActiveEffectCount()
    {
        var count = 0;
        _world.Query(in _effectEntities, (ref TimedEffect timed, ref EffectType type, ref EffectTarget target, ref EffectMagnitude magnitude) =>
        {
            if (timed.RemainingTime > 0)
            {
                count++;
            }
        });

        return count;
    }

    public int GetActiveProtection(Entity target)
    {
        var protection = 0;
        _world.Query(in _effectEntities, (ref TimedEffect timed, ref EffectType effectType, ref EffectTarget effectTarget, ref EffectMagnitude magnitude) =>
        {
            if (timed.RemainingTime <= 0)
            {
                return;
            }

            if (effectType.Value != EffectKind.Protection || effectTarget.Value != target)
            {
                return;
            }

            protection += Math.Max(0, magnitude.Value);
        });

        return protection;
    }

    public void CreateEffect(Entity target, EffectKind kind, int durationTime, int tickIntervalTime, int magnitude, int? timeUntilNextTickOverride = null)
    {
        var safeDuration = Math.Max(1, durationTime);
        var safeInterval = Math.Max(0, tickIntervalTime);
        var safeTimeUntilNextTick = safeInterval;
        if (safeInterval > 0 && timeUntilNextTickOverride.HasValue)
        {
            safeTimeUntilNextTick = Math.Max(1, timeUntilNextTickOverride.Value);
        }
        else if (safeInterval == 0)
        {
            safeTimeUntilNextTick = 0;
        }

        _world.Create(
            new TimedEffect
            {
                RemainingTime = safeDuration,
                TickInterval = safeInterval,
                TimeUntilNextTick = safeTimeUntilNextTick
            },
            new EffectType { Value = kind },
            new EffectTarget { Value = target },
            new EffectMagnitude { Value = Math.Max(0, magnitude) });
    }

    private static bool ApplyEffectTick(EffectKind kind, Entity target, int magnitude, Func<Entity, int, int> applyDamage)
    {
        return kind switch
        {
            EffectKind.Poison => applyDamage(target, magnitude) > 0,
            _ => false
        };
    }
}
