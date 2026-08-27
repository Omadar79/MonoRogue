using Arch.Core;
using MonoRogue.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class DungeonDepthTests
{
    private const int MapWidth = 30;
    private const int MapHeight = 20;

    [Fact]
    public void LevelOne_HasDownStairsButNoUpStairs()
    {
        using var map = new GameSession(MapWidth, MapHeight, seed: 42);
        ClearAllExceptPlayerAndStairs(map);

        var stairs = FindStairs(map);

        if (!stairs.TryGetValue(StairDirection.Down, out _))
        {
            throw new InvalidOperationException("Expected a down-staircase on level 1.");
        }

        if (stairs.TryGetValue(StairDirection.Up, out _))
        {
            throw new InvalidOperationException("Level 1 should not have an up-staircase.");
        }
    }

    [Fact]
    public void SteppingOnDownStairs_DescendsToNewLevel()
    {
        using var map = new GameSession(MapWidth, MapHeight, seed: 42);
        ClearAllExceptPlayerAndStairs(map);
        ClearAllMonsters(map);

        var down = FindStairs(map)[StairDirection.Down];
        var result = StepOntoStairs(map, down);

        if (!result.LevelChanged || result.Depth != 2)
        {
            throw new InvalidOperationException($"Expected a level change to depth 2, got depth {result.Depth}.");
        }

        if (map.GetDepth() != 2)
        {
            throw new InvalidOperationException($"Expected session depth 2, got {map.GetDepth()}.");
        }

        // Level 2 must have an up-stair and (since depth < max) a down-stair.
        var stairs = FindStairs(map);
        if (!stairs.TryGetValue(StairDirection.Up, out var up))
        {
            throw new InvalidOperationException("Expected an up-staircase on level 2.");
        }

        if (!stairs.TryGetValue(StairDirection.Down, out _))
        {
            throw new InvalidOperationException("Expected a down-staircase on level 2.");
        }

        // The player arrives on the up-stair of the new level.
        var player = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player.");
        if (player.X != up.X || player.Y != up.Y)
        {
            throw new InvalidOperationException("Expected the player to arrive on the up-staircase.");
        }
    }

    [Fact]
    public void AscendingToVisitedLevel_RestoresItsState()
    {
        using var map = new GameSession(MapWidth, MapHeight, seed: 42);
        ClearAllExceptPlayerAndStairs(map);
        ClearAllMonsters(map);

        // Descend to level 2.
        var down = FindStairs(map)[StairDirection.Down];
        StepOntoStairs(map, down);
        if (map.GetDepth() != 2)
        {
            throw new InvalidOperationException("Expected to be on depth 2 before ascending.");
        }

        // Ascend back to level 1.
        var up = FindStairs(map)[StairDirection.Up];
        var result = StepOntoStairs(map, up);

        if (!result.LevelChanged || result.Depth != 1)
        {
            throw new InvalidOperationException($"Expected a level change to depth 1, got depth {result.Depth}.");
        }

        // Level 1's down-stair must be where it originally was (state restored from cache, not regenerated).
        var stairs = FindStairs(map);
        if (!stairs.TryGetValue(StairDirection.Down, out var restoredDown) || restoredDown != down)
        {
            throw new InvalidOperationException("Expected level 1's down-staircase to be restored at its original position.");
        }

        // And the player should be standing on it (arrival cell of the cached level).
        var player = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player.");
        if (player.X != down.X || player.Y != down.Y)
        {
            throw new InvalidOperationException($"Expected the player back at the down-stair ({down}), found ({player.X},{player.Y}).");
        }
    }

    [Fact]
    public void PlayerStatsAndInventory_CarryAcrossLevelChanges()
    {
        using var map = new GameSession(MapWidth, MapHeight, seed: 42);
        ClearAllExceptPlayerAndStairs(map);
        ClearAllMonsters(map);

        var (hpBefore, maxHpBefore) = map.GetPlayerHealth();
        var goldBefore = map.GetGold();
        var xpBefore = map.GetExperience();

        var down = FindStairs(map)[StairDirection.Down];
        var result = StepOntoStairs(map, down);

        if (!result.LevelChanged)
        {
            throw new InvalidOperationException("Expected a level change.");
        }

        var (hpAfter, maxHpAfter) = map.GetPlayerHealth();
        if (hpAfter != hpBefore || maxHpAfter != maxHpBefore)
        {
            throw new InvalidOperationException($"Expected HP to carry across levels ({hpBefore}/{maxHpBefore} -> {hpAfter}/{maxHpAfter}).");
        }

        if (map.GetGold() != goldBefore)
        {
            throw new InvalidOperationException("Expected gold to carry across levels.");
        }

        if (map.GetExperience() != xpBefore)
        {
            throw new InvalidOperationException("Expected experience to carry across levels.");
        }
    }

    [Fact]
    public void SaveLoad_PreservesDepthAndLevelCache()
    {
        using var map = new GameSession(MapWidth, MapHeight, seed: 42);
        ClearAllExceptPlayerAndStairs(map);
        ClearAllMonsters(map);

        var down = FindStairs(map)[StairDirection.Down];
        StepOntoStairs(map, down);
        if (map.GetDepth() != 2)
        {
            throw new InvalidOperationException("Expected depth 2 before saving.");
        }

        var saved = map.SaveMap();

        using var reloaded = new GameSession(MapWidth, MapHeight, seed: 7);
        reloaded.LoadMap(saved);

        if (reloaded.GetDepth() != 2)
        {
            throw new InvalidOperationException($"Expected depth 2 after loading, got {reloaded.GetDepth()}.");
        }

        if (saved.Version != MapSerializer.CurrentSaveVersion)
        {
            throw new InvalidOperationException($"Expected save version {MapSerializer.CurrentSaveVersion}, got {saved.Version}.");
        }

        // Level 1 must be in the cache.
        if (saved.Levels == null || saved.Levels.Count == 0)
        {
            throw new InvalidOperationException("Expected cached levels in the save.");
        }

        // Walking back up from the loaded state must restore level 1 with its stairs.
        var up = FindStairs(reloaded)[StairDirection.Up];
        var result = StepOntoStairs(reloaded, up);
        if (!result.LevelChanged || result.Depth != 1)
        {
            throw new InvalidOperationException("Expected to ascend to depth 1 after loading the save.");
        }

        var stairs = FindStairs(reloaded);
        if (!stairs.TryGetValue(StairDirection.Down, out var restoredDown) || restoredDown != down)
        {
            throw new InvalidOperationException("Expected level 1's down-staircase to be restored after save/load.");
        }
    }

    [Fact]
    public void BottomDepth_HasNoDownStairs_AndAscendingWorks()
    {
        using var map = new GameSession(MapWidth, MapHeight, seed: 42);
        ClearAllExceptPlayerAndStairs(map);
        ClearAllMonsters(map);

        var maxDepth = map.GetMaxDepth();

        // Descend to the bottom.
        for (int depth = 1; depth < maxDepth; depth++)
        {
            var down = FindStairs(map)[StairDirection.Down];
            StepOntoStairs(map, down);
        }

        if (map.GetDepth() != maxDepth)
        {
            throw new InvalidOperationException($"Expected depth {maxDepth}, got {map.GetDepth()}.");
        }

        // The bottom level has no down-stairs; stepping on the up-stair must ascend.
        var stairs = FindStairs(map);
        if (stairs.TryGetValue(StairDirection.Down, out _))
        {
            throw new InvalidOperationException("The bottom level should not have a down-staircase.");
        }

        var up = stairs[StairDirection.Up];
        var result = StepOntoStairs(map, up);

        if (map.GetDepth() != maxDepth - 1)
        {
            throw new InvalidOperationException($"Expected to ascend to depth {maxDepth - 1}, got {map.GetDepth()}.");
        }
    }

    [Fact]
    public void LevelSeed_IsDeterministicPerDepth()
    {
        if (GameSession.LevelSeed(42, 2) != GameSession.LevelSeed(42, 2))
        {
            throw new InvalidOperationException("Level seeds for the same (seed, depth) must match.");
        }

        if (GameSession.LevelSeed(42, 2) == GameSession.LevelSeed(42, 3))
        {
            throw new InvalidOperationException("Level seeds should differ across depths.");
        }
    }

    [Fact]
    public void LazyGeneration_DeeperLevelRegeneratesIdentically()
    {
        // Two sessions with the same seed must generate the same level 2 layout.
        using var mapA = new GameSession(MapWidth, MapHeight, seed: 1234);
        using var mapB = new GameSession(MapWidth, MapHeight, seed: 1234);
        ClearAllExceptPlayerAndStairs(mapA);
        ClearAllExceptPlayerAndStairs(mapB);
        ClearAllMonsters(mapA);
        ClearAllMonsters(mapB);

        var downA = FindStairs(mapA)[StairDirection.Down];
        var downB = FindStairs(mapB)[StairDirection.Down];
        if (downA != downB)
        {
            throw new InvalidOperationException("Same seed should place the down-stair identically.");
        }

        StepOntoStairs(mapA, downA);
        StepOntoStairs(mapB, downB);

        var stairsA = FindStairs(mapA)[StairDirection.Up];
        var stairsB = FindStairs(mapB)[StairDirection.Up];
        if (stairsA != stairsB)
        {
            throw new InvalidOperationException("Same seed should generate the same level 2 up-stair position.");
        }
    }

    // ---- helpers ----

    private static Dictionary<StairDirection, Point> FindStairs(GameSession map)
    {
        var result = new Dictionary<StairDirection, Point>();
        var query = new QueryDescription().WithAll<Position, Stairs>();
        map.GetWorld().Query(in query, (ref Position pos, ref Stairs stairs) =>
        {
            result[stairs.Direction] = pos.Value;
        });
        return result;
    }

    // Teleports the player to a walkable, unblocked cell adjacent to the stairs and steps
    // onto them, so the level-change path runs exactly as it would in normal play.
    private static TurnResult StepOntoStairs(GameSession map, Point stairs)
    {
        var deltas = new[]
        {
            new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1),
            new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1)
        };

        foreach (var delta in deltas)
        {
            var adjacent = stairs + delta;
            if (!map.IsValidCell(adjacent) || map.IsBlocked(adjacent))
            {
                continue;
            }

            TeleportPlayer(map, adjacent);
            return map.ProcessPlayerTurn(stairs - adjacent);
        }

        throw new InvalidOperationException($"No walkable cell adjacent to stairs at {stairs}.");
    }

    private static void TeleportPlayer(GameSession map, Point destination)
    {
        var query = new QueryDescription().WithAll<Position, ActorControlled>();
        map.GetWorld().Query(in query, (ref Position pos, ref ActorControlled actor) =>
        {
            if (actor.Kind == ActorKind.Player)
            {
                pos.Value = destination;
            }
        });
    }

    private static void ClearAllExceptPlayerAndStairs(GameSession map)
    {
        var toDestroy = new HashSet<Entity>();
        var renderQuery = new QueryDescription().WithAll<Position, RenderGlyph>();
        map.GetWorld().Query(in renderQuery, (Entity entity, ref Position pos, ref RenderGlyph glyph) =>
        {
            if (glyph.Value.Glyph == '@')
            {
                return;
            }

            if (map.GetStairsAt(pos.Value) is not null)
            {
                return;
            }

            toDestroy.Add(entity);
        });

        foreach (var entity in toDestroy)
        {
            map.GetWorld().Destroy(entity);
        }
    }

    private static void ClearAllMonsters(GameSession map)
    {
        var toDestroy = new HashSet<Entity>();
        var actorQuery = new QueryDescription().WithAll<ActorControlled>();
        map.GetWorld().Query(in actorQuery, (Entity entity, ref ActorControlled actor) =>
        {
            if (actor.Kind == ActorKind.Monster)
            {
                toDestroy.Add(entity);
            }
        });

        foreach (var entity in toDestroy)
        {
            map.GetWorld().Destroy(entity);
        }
    }
}
