using Arch.Core;
using MonoRogue.Core;
using MonoRogue.Core.Systems;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class PathfindingTests
{
    [Fact]
    public void PathfindingService_ReturnsCardinalStepTowardTarget()
    {
        using var world = World.Create();
        var spatial = new SpatialMap(world, 9, 9);
        var pathfinding = new PathfindingService(spatial, world);

        var next = pathfinding.GetNextStep(new Point(4, 4), new Point(6, 4));

        if (next is not Point step || step != new Point(5, 4))
        {
            throw new InvalidOperationException($"Expected step (5,4) but got {next}.");
        }
    }

    [Fact]
    public void PathfindingService_RoutesAroundWall()
    {
        using var world = World.Create();
        var spatial = new SpatialMap(world, 3, 3);
        var pathfinding = new PathfindingService(spatial, world);

        // A wall sits directly between the mover and the target, forcing a detour downward.
        spatial.GetTileMap().SetTile(1, 0, TileKind.Wall);

        var next = pathfinding.GetNextStep(new Point(0, 0), new Point(2, 0));

        if (next is not Point step || step != new Point(0, 1))
        {
            throw new InvalidOperationException($"Expected the mover to step down around the wall (0,1) but got {next}.");
        }
    }

    [Fact]
    public void PathfindingService_ReturnsNullWhenUnreachable()
    {
        using var world = World.Create();
        var spatial = new SpatialMap(world, 3, 3);
        var pathfinding = new PathfindingService(spatial, world);

        // A full wall column separates the mover from the target.
        for (int y = 0; y < 3; y++)
        {
            spatial.GetTileMap().SetTile(1, y, TileKind.Wall);
        }

        if (pathfinding.GetNextStep(new Point(0, 0), new Point(2, 0)) is Point)
        {
            throw new InvalidOperationException("Expected no path through a solid wall.");
        }
    }

    [Fact]
    public void PathfindingService_IgnoresSelfAndTargetObstacles()
    {
        using var world = World.Create();
        var spatial = new SpatialMap(world, 5, 5);
        var pathfinding = new PathfindingService(spatial, world);

        // Both the mover and the target occupy cells (they are blocking entities), yet a
        // path must still be found because the pathfinder ignores those two cells.
        world.Create(new Position(new Point(0, 0)), new BlocksMovement());
        world.Create(new Position(new Point(4, 0)), new BlocksMovement());

        var next = pathfinding.GetNextStep(new Point(0, 0), new Point(4, 0));

        if (next is not Point step || step != new Point(1, 0))
        {
            throw new InvalidOperationException($"Expected step (1,0) but got {next}.");
        }
    }

    [Fact]
    public void Monster_ChasesAndAttacksPlayerItCanSee()
    {
        using var map = new GameSession(11, 7);
        var player = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player.");
        ClearAllExceptPlayer(map);

        // Open line of sight: the monster starts a few cells left of the player.
        var monsterStart = new Point(player.X - 3, player.Y);
        SpawnMonster(map, monsterStart, health: 20, damage: 1);

        var startHealth = map.GetPlayerHealth().Current;
        var reachedPlayer = false;

        // The monster gains one action per rest turn and advances toward the visible player.
        for (int turn = 0; turn < 10 && !reachedPlayer; turn++)
        {
            map.ProcessPlayerTurn(Point.None);

            if (FindMonsterPosition(map) is Point monsterPos && !map.GetTileMap().IsWalkable(monsterPos))
            {
                throw new InvalidOperationException($"Monster moved onto a non-walkable cell ({monsterPos.X},{monsterPos.Y}).");
            }

            if (map.GetPlayerHealth().Current < startHealth)
            {
                reachedPlayer = true;
            }
        }

        if (!reachedPlayer)
        {
            throw new InvalidOperationException("Expected the monster to chase and attack the player it can see.");
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
