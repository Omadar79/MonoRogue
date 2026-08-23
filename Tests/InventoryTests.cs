using Arch.Core;
using MonoRogue.Core;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class InventoryTests
{
    [Fact]
    public void Inventory_UseSelectedPotionHealsAndConsumesOnlyThatStack()
    {
        using var map = new GameSession(10, 10);
        ClearAllExceptPlayer(map);

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        SpawnItem(map, new Point(start.X + 1, start.Y), ItemKind.Potion, "potion", 8);
        SpawnItem(map, new Point(start.X + 2, start.Y), ItemKind.Potion, "potion", 5);

        map.ProcessPlayerTurn(new Point(1, 0));
        map.ProcessPlayerTurn(new Point(1, 0));

        if (map.GetInventory().Count != 2) throw new InvalidOperationException($"Expected 2 inventory stacks but got {map.GetInventory().Count}.");

        SetPlayerHealth(map, 5);

        var result = map.ProcessUseItemAt(1);

        if (!result.PotionUsed) throw new InvalidOperationException("Expected the selected potion to be used.");
        if (result.UsedItemName != "potion") throw new InvalidOperationException("Expected used item name to be potion.");
        if (result.HealAmount != 5) throw new InvalidOperationException($"Expected 5 healing but got {result.HealAmount}.");
        if (map.GetPlayerHealth().Current != 10) throw new InvalidOperationException($"Expected health 10 but got {map.GetPlayerHealth().Current}.");

        if (map.GetInventory().Count != 1) throw new InvalidOperationException($"Expected 1 remaining stack but got {map.GetInventory().Count}.");
        if (map.GetInventory()[0].Magnitude != 8) throw new InvalidOperationException($"Expected the magnitude-8 potion to remain but got {map.GetInventory()[0].Magnitude}.");
    }

    [Fact]
    public void Inventory_GoldIsNotUsable()
    {
        using var map = new GameSession(10, 10);
        ClearAllExceptPlayer(map);

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        SpawnItem(map, new Point(start.X + 1, start.Y), ItemKind.Gold, "gold", 1);
        map.ProcessPlayerTurn(new Point(1, 0));

        if (map.GetInventory().Count != 1) throw new InvalidOperationException($"Expected 1 inventory stack but got {map.GetInventory().Count}.");

        var result = map.ProcessUseItemAt(0);

        if (result.PotionUsed) throw new InvalidOperationException("Gold should not be usable.");
        if (result.HealAmount != 0) throw new InvalidOperationException("Gold should not heal.");
        if (map.GetInventory().Count != 1) throw new InvalidOperationException("Gold should remain in inventory after a failed use.");
    }

    [Fact]
    public void GameMain_InventoryModalStateTransitions()
    {
        var game = new GameMain();

        game.OpenInventory();
        if (game.GetCurrentState() != GameState.MainMenu) throw new InvalidOperationException("Inventory should not open from the main menu.");

        game.StartNewGame(10, 10);
        game.OpenInventory();
        if (game.GetCurrentState() != GameState.Inventory) throw new InvalidOperationException("Expected the inventory modal to open during gameplay.");

        game.CloseInventory();
        if (game.GetCurrentState() != GameState.Playing) throw new InvalidOperationException("Expected to resume playing after closing inventory.");
    }

    private static void SpawnItem(GameSession map, Point position, ItemKind kind, string name, int magnitude)
    {
        map.GetWorld().Create(
            new Position(position),
            RenderGlyph.FromArgb('!', unchecked((int)0xFF00FF00), unchecked((int)0xFF000000)),
            new Item { Kind = kind, Name = name, Magnitude = magnitude });
    }

    private static void SetPlayerHealth(GameSession map, int value)
    {
        var query = new QueryDescription().WithAll<ActorControlled, Health>();
        map.GetWorld().Query(in query, (ref ActorControlled actor, ref Health health) =>
        {
            if (actor.Kind == ActorKind.Player)
            {
                health.Current = value;
            }
        });
    }

    private static void ClearAllExceptPlayer(GameSession map)
    {
        var toDestroy = new HashSet<Entity>();

        var actorQuery = new QueryDescription().WithAll<ActorControlled>();
        map.GetWorld().Query(in actorQuery, (Entity entity, ref ActorControlled actor) =>
        {
            if (actor.Kind == ActorKind.Monster) toDestroy.Add(entity);
        });

        var renderQuery = new QueryDescription().WithAll<Position, RenderGlyph>();
        map.GetWorld().Query(in renderQuery, (Entity entity, ref RenderGlyph glyph) =>
        {
            if (glyph.Value.Glyph != '@') toDestroy.Add(entity);
        });

        foreach (var entity in toDestroy)
        {
            map.GetWorld().Destroy(entity);
        }
    }
}
