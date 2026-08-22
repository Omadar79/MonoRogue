using Arch.Core;

namespace MonoRogue.Core.Systems;

/// <summary>
/// Tracks each actor's energy: accrual each turn and the player's action cost.
/// </summary>
public sealed class EnergySystem
{
    private readonly World _world;
    private readonly QueryDescription _actorEntities;
    private readonly QueryDescription _playerEntities;

    public EnergySystem(World world)
    {
        _world = world;
        _actorEntities = new QueryDescription().WithAll<Position, ActorControlled, Energy, RenderGlyph>();
        _playerEntities = new QueryDescription().WithAll<ActorControlled, Energy>();
    }

    public void AdvanceActorEnergy()
    {
        _world.Query(in _actorEntities, (ref Energy energy) =>
        {
            if (energy.GainPerTurn > 0)
            {
                energy.Current += energy.GainPerTurn;
            }
        });
    }

    public bool TryConsumePlayerEnergy()
    {
        var consumed = false;
        _world.Query(in _playerEntities, (ref ActorControlled actor, ref Energy energy) =>
        {
            if (consumed || actor.Kind != ActorKind.Player)
            {
                return;
            }

            var actionCost = Math.Max(1, energy.ActionCost);
            if (energy.Current < actionCost)
            {
                return;
            }

            energy.Current -= actionCost;
            consumed = true;
        });

        return consumed;
    }
}
