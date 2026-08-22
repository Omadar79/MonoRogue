using Arch.Core;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Populates the world with entities: the player, monsters, items, and treasure.
/// Reads content definitions from the <see cref="MonoRogue.Data"/> loaders and writes
/// entities into the <see cref="World"/>. Placement uses <see cref="Random.Shared"/> with
/// bounded retry loops; callers that need determinism should seed the RNG.
/// </summary>
public sealed class MapGenerator
{
    private readonly World _world;
    private readonly SpatialMap _spatial;
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    public MapGenerator(World world, SpatialMap spatial, int mapWidth, int mapHeight)
    {
        _world = world;
        _spatial = spatial;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
    }

    public void GenerateNewMap()
    {
        CreateTreasure();

        var itemTemplates = ItemDataLoader.LoadDefinitionsFromDefaultSearchPaths(Directory.GetCurrentDirectory());
        foreach (var template in itemTemplates)
        {
            CreateItem(template);
        }

        var templates = MonsterDataLoader.LoadDefinitionsFromDefaultSearchPaths(Directory.GetCurrentDirectory());
        if (templates.Count == 0)
        {
            CreateGoblin();
            CreateDragon();
        }
        else
        {
            foreach (var template in templates)
            {
                CreateMonster(template);
            }
        }
    }

    public void CreateInitialPlayer()
    {
        var center = _spatial.Center;
        _world.Create(new Position(center),
            RenderGlyph.FromArgb('@', GameConstants.ArgbWhite, GameConstants.ArgbBlack),
            new ActorControlled { Kind = ActorKind.Player },
            new BlocksMovement(),
            new Health { Current = GameConstants.DefaultPlayerHealth, Max = GameConstants.DefaultPlayerHealth },
            new Attack { Damage = GameConstants.DefaultPlayerAttack },
            new Energy { Current = GameConstants.DefaultActionCost, GainPerTurn = GameConstants.DefaultEnergyPerTurn, ActionCost = GameConstants.DefaultActionCost });
    }

    private void CreateItem(ItemDefinition definition)
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (_spatial.IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb),
                new Item
                {
                    Kind = definition.Kind,
                    Name = definition.Name,
                    Magnitude = Math.Max(1, definition.Magnitude)
                });

            break;
        }
    }

    private void CreateMonster(MonsterDefinition definition)
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (_spatial.IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb),
                new Health { Current = GameConstants.DefaultMonsterHealth, Max = GameConstants.DefaultMonsterHealth },
                new Attack { Damage = Math.Max(1, definition.Damage) },
                new BlocksMovement(),
                new ActorControlled { Kind = ActorKind.Monster },
                new MonsterBehavior { Type = definition.Behavior, Range = definition.Range, SpecialEnergyCost = definition.SpecialEnergyCost },
                new Energy
                {
                    Current = 0,
                    GainPerTurn = Math.Max(1, definition.GainPerTurn),
                    ActionCost = Math.Max(1, definition.ActionCost)
                });

            break;
        }
    }

    private void CreateTreasure()
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (_spatial.IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition), RenderGlyph.FromArgb('v', GameConstants.ArgbYellow, GameConstants.ArgbBlack), new BlocksMovement());
            break;
        }
    }

    private void CreateGoblin()
    {
        CreateMonster('g', GameConstants.ArgbRed, GameConstants.DefaultEnergyPerTurn, GameConstants.DefaultActionCost);
    }

    private void CreateDragon()
    {
        // Dragon gains energy like a normal actor but can spend more on special actions.
        CreateMonster('D', GameConstants.ArgbOrangeRed, GameConstants.DefaultEnergyPerTurn, GameConstants.DefaultActionCost);
    }

    private void CreateMonster(int glyphCode, int foregroundArgb, int gainPerTurn, int actionCost)
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (_spatial.IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb((char)glyphCode, foregroundArgb, GameConstants.ArgbBlack),
                new Health { Current = GameConstants.DefaultMonsterHealth, Max = GameConstants.DefaultMonsterHealth },
                new Attack { Damage = glyphCode == 'D' ? GameConstants.DragonAttack : GameConstants.DefaultMonsterAttack },
                new BlocksMovement(),
                new ActorControlled { Kind = ActorKind.Monster },
                InferBehavior((char)glyphCode),
                new Energy
                {
                    Current = 0,
                    GainPerTurn = Math.Max(1, gainPerTurn),
                    ActionCost = Math.Max(1, actionCost)
                });

            break;
        }
    }

    // Infer monster AI from a legacy glyph when no JSON definition provides behavior.
    // Used only for the no-content fallback and for legacy saved maps.
    internal static MonsterBehavior InferBehavior(char glyph)
    {
        return glyph == 'D'
            ? new MonsterBehavior { Type = MonsterAIType.Breath, Range = 3, SpecialEnergyCost = 300 }
            : new MonsterBehavior { Type = MonsterAIType.Melee, Range = 1, SpecialEnergyCost = 0 };
    }
}
