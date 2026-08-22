using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Core.Systems;

/// <summary>
/// Player-only logic: locating the player entity and moving/resting it.
/// </summary>
public sealed class PlayerActionSystem
{
    private readonly World _world;
    private readonly SpatialMap _spatial;
    private readonly QueryDescription _playerMarkerEntities;
    private readonly QueryDescription _playerPositionEntities;
    private readonly QueryDescription _playerMoveEntities;

    public PlayerActionSystem(World world, SpatialMap spatial)
    {
        _world = world;
        _spatial = spatial;
        _playerMarkerEntities = new QueryDescription().WithAll<ActorControlled>();
        _playerPositionEntities = new QueryDescription().WithAll<Position, ActorControlled>();
        _playerMoveEntities = new QueryDescription().WithAll<Position, ActorControlled, Energy>();
    }

    public bool TryGetPlayerEntity(out Entity playerEntity)
    {
        var result = default(Entity);
        var found = false;
        _world.Query(in _playerMarkerEntities, (Entity entity, ref ActorControlled actor) =>
        {
            if (found || actor.Kind != ActorKind.Player)
            {
                return;
            }

            result = entity;
            found = true;
        });

        playerEntity = result;
        return found;
    }

    public Point? GetPlayerPosition()
    {
        Point? result = null;
        _world.Query(in _playerPositionEntities, (ref Position position, ref ActorControlled actor) =>
        {
            if (actor.Kind != ActorKind.Player)
            {
                return;
            }

            result ??= position.Value;
        });

        return result;
    }

    public bool TryMovePlayerNoRefresh(Point offset)
    {
        var moved = false;
        _world.Query(in _playerMoveEntities, (ref Position position, ref ActorControlled actor, ref Energy energy) =>
        {
            if (actor.Kind != ActorKind.Player)
            {
                return;
            }

            var actionCost = Math.Max(1, energy.ActionCost);
            if (energy.Current < actionCost)
            {
                return;
            }

            if (offset == Point.None)
            {
                energy.Current -= actionCost;
                return;
            }

            var destination = position.Value + offset;
            if (!_spatial.IsValidCell(destination) || _spatial.IsBlocked(destination))
            {
                return;
            }

            position.Value = destination;
            energy.Current -= actionCost;
            moved = true;
        });

        return moved;
    }
}
