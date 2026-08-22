using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Core;

public class Player
{
    private const int ArgbWhite = unchecked((int)0xFFFFFFFF);
    private const int ArgbBlack = unchecked((int)0xFF000000);
    private const int DefaultEnergyPerTurn = 100;
    private const int DefaultActionCost = 100;

    private readonly QueryDescription _playerEntities;

    private readonly World _world;

    private readonly MapBase _currentMap;

    public Player(World world, MapBase currentMap)
    {
        _world = world;
        _playerEntities = new QueryDescription().WithAll<Position, ActorControlled, RenderGlyph, Energy>();

        _currentMap = currentMap;

        CreatePlayer(_currentMap.GetMapCenter());
    }
    
    public bool TryMovePlayer(Point offset)
    {
        return TryPlayerAction(offset);
    }

    public bool TryRestPlayer()
    {
        return TryPlayerAction(Point.None);
    }

    private bool TryPlayerAction(Point offset)
    {
        var acted = false;

        _world.Query(in _playerEntities, (ref Position position, ref RenderGlyph renderGlyph, ref Energy energy) =>
        {
            var actionCost = Math.Max(1, energy.ActionCost);
            if (energy.Current < actionCost)
            {
                return;
            }

            if (offset != Point.None)
            {
                var destination = position.Value + offset;
                if (!_currentMap.IsValidCell(destination) || _currentMap.IsBlocked(destination))
                {
                    return;
                }

                position.Value = destination;
            }

            energy.Current -= actionCost;
            acted = true;
        });

        return acted;
    }
    
    private void CreatePlayer(Point position)
    {
            _world.Create(new Position(position), RenderGlyph.FromArgb('@', ArgbWhite, ArgbBlack), new ActorControlled { Kind = ActorKind.Player }, new BlocksMovement(), new Energy { Current = DefaultActionCost, GainPerTurn = DefaultEnergyPerTurn, ActionCost = DefaultActionCost });
    }
}


