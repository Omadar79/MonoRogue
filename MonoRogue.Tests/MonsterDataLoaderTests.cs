using MonoRogue.Data;
using MonoRogue.Core;
using Arch.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public static class MonsterDataLoaderTests
{
    public static void RunAll()
    {
        LoadDefinitions_FromJsonFile_ReturnsMonsterDefinitions();
        LoadItemDefinitions_FromJsonFile_ReturnsItemDefinitions();
        Player_CanBeTreatedAsEntity();
        Player_CanActRepeatedlyWhilePoisoned();
        Player_CanRestAndContinueActingWhilePoisoned();
    }

    public static void LoadDefinitions_FromJsonFile_ReturnsMonsterDefinitions()
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
              "actionCost": 100
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
    }

    public static void LoadItemDefinitions_FromJsonFile_ReturnsItemDefinitions()
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
      "backgroundArgb": -16777216
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
    }

    public static void Player_CanBeTreatedAsEntity()
    {
        using var map = new MapBase(10, 10);

        // Apply a light effect to the player and verify an effect entity is created.
        var applied = map.TryApplyLightToPlayer(10, 1);
        if (!applied) throw new InvalidOperationException("Expected to apply light to player.");

        var count = map.GetActiveEffectCount();
        if (count <= 0) throw new InvalidOperationException($"Expected active effect count > 0 but got {count}.");
    }

    public static void Player_CanActRepeatedlyWhilePoisoned()
    {
        using var map = new MapBase(10, 10);

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        var path = new HashSet<Point>
        {
            new(start.X + 1, start.Y),
            new(start.X + 2, start.Y),
            new(start.X + 3, start.Y)
        };

        ClearBlockingEntities(map, path);

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

    public static void Player_CanRestAndContinueActingWhilePoisoned()
    {
        using var map = new MapBase(10, 10);

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        ClearBlockingEntities(map, new HashSet<Point> { new(start.X + 1, start.Y) });

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
