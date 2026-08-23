using Arch.Core;
using MonoRogue.Core;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class MonsterAggroTests
{
    [Fact]
    public void Monster_DoesNotChasePlayerItCannotSee()
    {
        using var map = new GameSession(11, 7);
        var player = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player.");
        ClearAllExceptPlayer(map);

        // A solid wall column separates the monster from the player, blocking line of sight.
        for (int y = 1; y <= 5; y++)
        {
            map.GetTileMap().SetTile(3, y, TileKind.Wall);
        }

        var monsterStart = new Point(1, 3);
        SpawnMonster(map, monsterStart, health: 20, damage: 1);

        var startHealth = map.GetPlayerHealth().Current;

        for (int turn = 0; turn < 5; turn++)
        {
            map.ProcessPlayerTurn(Point.None);
        }

        var monsterPos = FindMonsterPosition(map);
        if (monsterPos is not Point position || position != monsterStart)
        {
            throw new InvalidOperationException($"Expected the monster to stay at {monsterStart} but it is at {monsterPos}.");
        }

        if (map.GetPlayerHealth().Current != startHealth)
        {
            throw new InvalidOperationException("Expected no damage from a monster that cannot see the player.");
        }
    }

    [Fact]
    public void Monster_MovesToLastSeenPositionAfterLosingSight()
    {
        using var map = new GameSession(11, 7);
        var player = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player.");
        ClearAllExceptPlayer(map);

        // The monster starts with clear line of sight to the player, so the first turn
        // refreshes its memory and advances it one step toward the player.
        var monsterStart = new Point(1, player.Y);
        SpawnMonster(map, monsterStart, health: 20, damage: 1);

        map.ProcessPlayerTurn(Point.None);
        if (FindMonsterPosition(map) is not Point afterFirst || afterFirst != new Point(2, player.Y))
        {
            throw new InvalidOperationException($"Expected the monster to advance to (2,{player.Y}) but it is at {FindMonsterPosition(map)}.");
        }

        // Relocate the player behind a wall so it leaves the monster's sight. The wall sits
        // on the line between the monster and the player's new position.
        SetPlayerPosition(map, new Point(1, 1));
        map.GetTileMap().SetTile(1, 2, TileKind.Wall);

        map.ProcessPlayerTurn(Point.None);

        // The monster should move toward its last-seen position (to the right), not toward
        // the player's new hidden location (up-left).
        if (FindMonsterPosition(map) is not Point final || final != new Point(3, player.Y))
        {
            throw new InvalidOperationException($"Expected the monster to head toward the last-seen position (3,{player.Y}) but it is at {FindMonsterPosition(map)}.");
        }
    }

    private static Point? FindMonsterPosition(GameSession map)
    {
        Point? result = null;
        var query = new QueryDescription().WithAll<Position, ActorControlled>();
        map.GetWorld().Query(in query, (ref Position pos, ref ActorControlled actor) =>
        {
            if (actor.Kind == ActorKind.Monster)
            {
                result ??= pos.Value;
            }
        });
        return result;
    }

    private static void SetPlayerPosition(GameSession map, Point position)
    {
        var query = new QueryDescription().WithAll<Position, ActorControlled>();
        map.GetWorld().Query(in query, (ref Position pos, ref ActorControlled actor) =>
        {
            if (actor.Kind == ActorKind.Player)
            {
                pos.Value = position;
            }
        });
    }

    private static void SpawnMonster(GameSession map, Point position, int health, int damage)
    {
        map.GetWorld().Create(
            new Position(position),
            RenderGlyph.FromArgb('g', unchecked((int)0xFFFF0000), unchecked((int)0xFF000000)),
            new Health { Current = health, Max = health },
            new BlocksMovement(),
            new ActorControlled { Kind = ActorKind.Monster },
            new MonsterBehavior { Type = MonsterAIType.Melee },
            new MonsterMemory(),
            new Energy { Current = 0, GainPerTurn = 100, ActionCost = 100 },
            new Attack { Damage = damage });
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
