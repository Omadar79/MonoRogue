using Arch.Core;
using MonoRogue.Core;
using SadRogue.Primitives;

namespace MonoRogue.Tests;

public class ExperienceTests
{
    [Fact]
    public void Monster_DeathAwardsExperience()
    {
        using var map = new GameSession(10, 10);
        ClearAllExceptPlayer(map);

        var start = map.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        SpawnMonster(map, new Point(start.X + 1, start.Y), health: 1, experience: 15);

        var turnResult = map.ProcessPlayerTurn(new Point(1, 0));

        if (!turnResult.MonsterKilled) throw new InvalidOperationException("Expected the monster to be killed.");
        if (turnResult.ExperienceGained != 15) throw new InvalidOperationException($"Expected 15 XP gained but got {turnResult.ExperienceGained}.");
        if (map.GetExperience() != 15) throw new InvalidOperationException($"Expected accumulated XP of 15 but got {map.GetExperience()}.");
    }

    [Fact]
    public void PlayerLevel_DerivedFromChart()
    {
        var experience = new PlayerExperience();

        if (experience.GetLevel() != 1) throw new InvalidOperationException($"Expected level 1 at 0 XP but got {experience.GetLevel()}.");

        experience.Award(20);
        if (experience.GetLevel() != 2) throw new InvalidOperationException($"Expected level 2 at 20 XP but got {experience.GetLevel()}.");

        experience.Award(30);
        if (experience.GetLevel() != 3) throw new InvalidOperationException($"Expected level 3 at 50 XP but got {experience.GetLevel()}.");
        if (experience.XpForNextLevel() != 100) throw new InvalidOperationException($"Expected next level threshold of 100 but got {experience.XpForNextLevel()}.");
    }

    [Fact]
    public void Experience_PersistsAcrossSaveAndLoad()
    {
        using var map1 = new GameSession(10, 10);
        ClearAllExceptPlayer(map1);

        var start = map1.ExtractPlayerState() ?? throw new InvalidOperationException("Expected a player state.");
        SpawnMonster(map1, new Point(start.X + 1, start.Y), health: 1, experience: 15);
        map1.ProcessPlayerTurn(new Point(1, 0));

        if (map1.GetExperience() != 15) throw new InvalidOperationException($"Expected 15 XP before saving but got {map1.GetExperience()}.");

        var data = map1.SaveMap();

        using var map2 = new GameSession(10, 10);
        map2.LoadMap(data);

        if (map2.GetExperience() != 15) throw new InvalidOperationException($"Expected 15 XP after loading but got {map2.GetExperience()}.");
        if (map2.GetPlayerLevel() != 1) throw new InvalidOperationException($"Expected level 1 after loading but got {map2.GetPlayerLevel()}.");
    }

    private static void SpawnMonster(GameSession map, Point position, int health, int experience)
    {
        map.GetWorld().Create(
            new Position(position),
            RenderGlyph.FromArgb('g', unchecked((int)0xFFFF0000), unchecked((int)0xFF000000)),
            new Health { Current = health, Max = health },
            new BlocksMovement(),
            new ActorControlled { Kind = ActorKind.Monster },
            new MonsterBehavior { Type = MonoRogue.Data.MonsterAIType.Melee },
            new Energy { Current = 0, GainPerTurn = 100, ActionCost = 100 },
            new Attack { Damage = 3 },
            new Experience { Value = experience });
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
