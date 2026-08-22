using MonoRogue.Data;
using MonoRogue.Core;
using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class MonsterDataLoaderTests
{
    [Fact]
    public void LoadDefinitions_FromJsonFile_ReturnsMonsterDefinitions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var path = Path.Combine(tempDir, "monsters.json");
        File.WriteAllText(path, """
        {
          "monsters": [
            {
              "name": "goblin",
              "glyph": "g",
              "foregroundArgb": -65536,
              "backgroundArgb": -16777216,
              "gainPerTurn": 100,
              "actionCost": 100,
              "damage": 3
            }
          ]
        }
        """);

        var definitions = MonsterDataLoader.LoadDefinitions(path);

        if (definitions.Count != 1)
        {
            throw new InvalidOperationException($"Expected 1 monster definition but got {definitions.Count}.");
        }

        if (definitions[0].Name != "goblin") throw new InvalidOperationException("Expected goblin name.");
        if (definitions[0].Glyph != 'g') throw new InvalidOperationException("Expected goblin glyph.");
        if (definitions[0].GainPerTurn != 100) throw new InvalidOperationException("Expected gainPerTurn of 100.");
        if (definitions[0].ActionCost != 100) throw new InvalidOperationException("Expected actionCost of 100.");
        if (definitions[0].Damage != 3) throw new InvalidOperationException("Expected damage of 3.");
    }

    [Fact]
    public void LoadDefinitions_FromJsonFile_ParsesMonsterBehavior()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var path = Path.Combine(tempDir, "monsters.json");
        File.WriteAllText(path, """
        {
          "monsters": [
            {
              "name": "dragon",
              "glyph": "D",
              "foregroundArgb": -23296,
              "backgroundArgb": -16777216,
              "gainPerTurn": 100,
              "actionCost": 100,
              "damage": 5,
              "behavior": "Breath",
              "range": 3,
              "specialEnergyCost": 300
            }
          ]
        }
        """);

        var definitions = MonsterDataLoader.LoadDefinitions(path);

        if (definitions.Count != 1) throw new InvalidOperationException($"Expected 1 monster definition but got {definitions.Count}.");
        if (definitions[0].Behavior != MonsterAIType.Breath) throw new InvalidOperationException("Expected Breath behavior.");
        if (definitions[0].Range != 3) throw new InvalidOperationException("Expected range of 3.");
        if (definitions[0].SpecialEnergyCost != 300) throw new InvalidOperationException("Expected specialEnergyCost of 300.");
    }

    [Fact]
    public void LoadItemDefinitions_FromJsonFile_ReturnsItemDefinitions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var path = Path.Combine(tempDir, "items.json");
        File.WriteAllText(path, """
{
  "items": [
    {
      "name": "potion",
      "glyph": "!",
      "foregroundArgb": -16711936,
      "backgroundArgb": -16777216,
      "kind": "Potion",
      "magnitude": 8
    }
  ]
}
""");

        var definitions = ItemDataLoader.LoadDefinitions(path);

        if (definitions.Count != 1)
        {
            throw new InvalidOperationException($"Expected 1 item definition but got {definitions.Count}.");
        }

        if (definitions[0].Name != "potion") throw new InvalidOperationException("Expected potion name.");
        if (definitions[0].Glyph != '!') throw new InvalidOperationException("Expected potion glyph.");
        if (definitions[0].ForegroundArgb != -16711936) throw new InvalidOperationException("Expected green foreground.");
        if (definitions[0].BackgroundArgb != -16777216) throw new InvalidOperationException("Expected black background.");
        if (definitions[0].Kind != ItemKind.Potion) throw new InvalidOperationException("Expected potion kind.");
        if (definitions[0].Magnitude != 8) throw new InvalidOperationException("Expected magnitude of 8.");
    }

    [Fact]
    public void Player_CanBeTreatedAsEntity()
    {
        using var map = new MapBase(10, 10);

        // Apply a light effect to the player and verify an effect entity is created.
        var applied = map.TryApplyLightToPlayer(10, 1);
        if (!applied) throw new InvalidOperationException("Expected to apply light to player.");

        var count = map.GetActiveEffectCount();
        if (count <= 0) throw new InvalidOperationException($"Expected active effect count > 0 but got {count}.");
    }

    [Fact]
    public void Player_CanActRepeatedlyWhilePoisoned()
    {
        using var map = new MapBase(10, 10);

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        ClearAllExceptPlayer(map);

        var applied = map.TryApplyPoisonToPlayer(500, 100, 1);
        if (!applied) throw new InvalidOperationException("Expected to apply poison to player.");

        for (var i = 0; i < 3; i++)
        {
            var turnResult = map.ProcessPlayerTurn(new Point(1, 0));
            if (!turnResult.PlayerMoved)
            {
                throw new InvalidOperationException($"Expected player to move on poisoned turn {i + 1}.");
            }

            var state = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state after moving.");
            var expectedX = start.X + i + 1;
            if (state.X != expectedX || state.Y != start.Y)
            {
                throw new InvalidOperationException($"Expected player at ({expectedX},{start.Y}) but got ({state.X},{state.Y}).");
            }
        }

        if (map.GetActiveEffectCount() <= 0)
        {
            throw new InvalidOperationException("Expected poison effect to remain active during the turns.");
        }
    }

    [Fact]
    public void Player_CanRestAndContinueActingWhilePoisoned()
    {
        using var map = new MapBase(10, 10);

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        ClearAllExceptPlayer(map);

        var applied = map.TryApplyPoisonToPlayer(500, 100, 1);
        if (!applied) throw new InvalidOperationException("Expected to apply poison to player.");

        var restTurn = map.ProcessPlayerTurn(Point.None);
        if (restTurn.PlayerMoved)
        {
            throw new InvalidOperationException("Expected resting to avoid movement.");
        }

        var rested = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state after resting.");
        if (rested.X != start.X || rested.Y != start.Y)
        {
            throw new InvalidOperationException($"Expected player to remain at ({start.X},{start.Y}) after resting but got ({rested.X},{rested.Y}).");
        }

        if (restTurn.EffectTicksProcessed <= 0)
        {
            throw new InvalidOperationException("Expected resting to advance active effects.");
        }

        var moveTurn = map.ProcessPlayerTurn(new Point(1, 0));
        if (!moveTurn.PlayerMoved)
        {
            throw new InvalidOperationException("Expected player to move after resting.");
        }

        var moved = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state after moving.");
        if (moved.X != start.X + 1 || moved.Y != start.Y)
        {
            throw new InvalidOperationException($"Expected player at ({start.X + 1},{start.Y}) but got ({moved.X},{moved.Y}).");
        }

        if (map.GetActiveEffectCount() <= 0)
        {
            throw new InvalidOperationException("Expected poison effect to remain active after resting and moving.");
        }
    }

    [Fact]
    public void Player_CanAttackAndKillMonsterByBumpingIntoIt()
    {
        using var map = new MapBase(10, 10);
        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        var target = new Point(start.X + 1, start.Y);

        ClearBlockingEntities(map, new HashSet<Point> { target });
        SpawnMonster(map, target, health: 1, damage: 3);

        var turnResult = map.ProcessPlayerTurn(new Point(1, 0));

        if (!turnResult.PlayerAttacked)
        {
            throw new InvalidOperationException("Expected player to attack an adjacent monster.");
        }

        if (!turnResult.MonsterKilled)
        {
            throw new InvalidOperationException("Expected the adjacent monster to be killed by the attack.");
        }

        if (turnResult.PlayerMoved)
        {
            throw new InvalidOperationException("Player should not move when attacking a monster.");
        }

        if (turnResult.PlayerDied)
        {
            throw new InvalidOperationException("Player should not die when killing a 1-health monster.");
        }
    }

    [Fact]
    public void Monster_MeleeAttackDamagesPlayer()
    {
        using var map = new MapBase(10, 10);
        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        var target = new Point(start.X + 1, start.Y);

        ClearBlockingEntities(map, new HashSet<Point> { target });
        SpawnMonster(map, target, health: 8, damage: 3);

        var healthBefore = map.GetHealthAt(new Point(start.X, start.Y));
        map.ProcessPlayerTurn(Point.None); // rest; the adjacent monster should melee

        var healthAfter = map.GetHealthAt(new Point(start.X, start.Y));

        if (healthAfter >= healthBefore)
        {
            throw new InvalidOperationException($"Expected player health to drop after adjacent monster melee, but went from {healthBefore} to {healthAfter}.");
        }
    }

    [Fact]
    public void Player_CanPickUpItemByWalkingOntoIt()
    {
        using var map = new MapBase(10, 10);
        ClearAllExceptPlayer(map);

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        var target = new Point(start.X + 1, start.Y);
        SpawnItem(map, target, ItemKind.Potion, "potion", 8);

        var turnResult = map.ProcessPlayerTurn(new Point(1, 0));

        if (!turnResult.PlayerMoved) throw new InvalidOperationException("Expected player to move onto the item.");
        if (!turnResult.ItemPickedUp) throw new InvalidOperationException("Expected to pick up the item.");
        if (turnResult.ItemPickedUpName != "potion") throw new InvalidOperationException("Expected potion pickup name.");

        var potionCount = map.Inventory.Count(i => i.Kind == ItemKind.Potion);
        if (potionCount != 1) throw new InvalidOperationException($"Expected 1 potion in inventory but got {potionCount}.");
    }

    [Fact]
    public void Player_CanUsePotionToHeal()
    {
        using var map = new MapBase(10, 10);
        ClearAllExceptPlayer(map);

        // Lower the player's health deterministically.
        var query = new QueryDescription().WithAll<ActorControlled, Health>();
        map.World.Query(in query, (ref ActorControlled actor, ref Health health) =>
        {
            if (actor.Kind == ActorKind.Player)
            {
                health.Current = 5;
            }
        });

        if (map.TryConsumePotion()) throw new InvalidOperationException("Expected no potion to be available yet.");

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        var target = new Point(start.X + 1, start.Y);
        SpawnItem(map, target, ItemKind.Potion, "potion", 8);

        var pickup = map.ProcessPlayerTurn(new Point(1, 0));
        if (!pickup.ItemPickedUp) throw new InvalidOperationException("Expected to pick up the potion.");

        var healthBefore = map.GetPlayerHealth().Current;
        if (healthBefore != 5) throw new InvalidOperationException($"Expected health 5 before healing but got {healthBefore}.");

        if (!map.TryConsumePotion()) throw new InvalidOperationException("Expected to consume the potion.");

        var healthAfter = map.GetPlayerHealth().Current;
        if (healthAfter != 13) throw new InvalidOperationException($"Expected health 13 after healing but got {healthAfter}.");

        var remaining = map.Inventory.Count(i => i.Kind == ItemKind.Potion);
        if (remaining != 0) throw new InvalidOperationException($"Expected 0 potions remaining but got {remaining}.");
    }

    private static void SpawnItem(MapBase map, Point position, ItemKind kind, string name, int magnitude)
    {
        map.World.Create(
            new Position(position),
            RenderGlyph.FromArgb('!', unchecked((int)0xFF00FF00), unchecked((int)0xFF000000)),
            new Item { Kind = kind, Name = name, Magnitude = magnitude });
    }

    private static void ClearAllExceptPlayer(MapBase map)
    {
        var toDestroy = new HashSet<Entity>();

        var actorQuery = new QueryDescription().WithAll<ActorControlled>();
        map.World.Query(in actorQuery, (Entity entity, ref ActorControlled actor) =>
        {
            if (actor.Kind == ActorKind.Monster) toDestroy.Add(entity);
        });

        var renderQuery = new QueryDescription().WithAll<Position, RenderGlyph>();
        map.World.Query(in renderQuery, (Entity entity, ref RenderGlyph glyph) =>
        {
            if (glyph.Value.Glyph != '@') toDestroy.Add(entity);
        });

        foreach (var entity in toDestroy)
        {
            map.World.Destroy(entity);
        }
    }

    private static void SpawnMonster(MapBase map, Point position, int health, int damage)
    {
        map.World.Create(
            new Position(position),
            RenderGlyph.FromArgb('g', unchecked((int)0xFFFF0000), unchecked((int)0xFF000000)),
            new Health { Current = health, Max = health },
            new BlocksMovement(),
            new ActorControlled { Kind = ActorKind.Monster },
            new MonsterBehavior { Type = MonsterAIType.Melee },
            new Energy { Current = 0, GainPerTurn = 100, ActionCost = 100 },
            new Attack { Damage = damage });
    }

    private static void ClearBlockingEntities(MapBase map, HashSet<Point> positions)
    {
        var blockers = new List<Entity>();
        var query = new QueryDescription().WithAll<Position, BlocksMovement>();
        map.World.Query(in query, (Entity entity, ref Position pos, ref BlocksMovement blocks) =>
        {
            if (positions.Contains(pos.Value))
            {
                blockers.Add(entity);
            }
        });

        foreach (var entity in blockers)
        {
            map.World.Destroy(entity);
        }
    }
}
