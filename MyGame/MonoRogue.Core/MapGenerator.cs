using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Populates the world with entities: the player, monsters, items, and treasure.
/// Reads content definitions from the <see cref="MonoRogue.Data"/> loaders and
/// delegates entity construction to <see cref="EntityFactory"/>. Placement uses
/// <see cref="Random.Shared"/> with bounded retry loops; callers that need
/// determinism should seed the RNG.
/// </summary>
public sealed class MapGenerator
{
    private readonly EntityFactory _factory;
    private readonly SpatialMap _spatial;
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    public MapGenerator(EntityFactory factory, SpatialMap spatial, int mapWidth, int mapHeight)
    {
        _factory = factory;
        _spatial = spatial;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
    }

    public void GenerateNewMap()
    {
        CreateTreasure();

        var itemTemplates = ItemDataLoader.LoadDefinitionsFromDefaultSearchPaths();
        foreach (var template in itemTemplates)
        {
            CreateItem(template);
        }

        var templates = MonsterDataLoader.LoadDefinitionsFromDefaultSearchPaths();
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
        _factory.CreatePlayer(
            center,
            new CoreGlyph('@', GameConstants.ArgbWhite, GameConstants.ArgbBlack),
            new Health { Current = GameConstants.DefaultPlayerHealth, Max = GameConstants.DefaultPlayerHealth },
            GameConstants.DefaultPlayerAttack);
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

            _factory.CreateItem(
                randomPosition,
                new CoreGlyph(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb),
                definition.Kind,
                definition.Name,
                definition.Magnitude);

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

            _factory.CreateMonster(
                randomPosition,
                new CoreGlyph(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb),
                new Health { Current = GameConstants.DefaultMonsterHealth, Max = GameConstants.DefaultMonsterHealth },
                Math.Max(1, definition.Damage),
                new MonsterBehavior { Type = definition.Behavior, Range = definition.Range, SpecialEnergyCost = definition.SpecialEnergyCost },
                definition.GainPerTurn,
                definition.ActionCost);

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

            _factory.CreateBlocker(randomPosition, new CoreGlyph('v', GameConstants.ArgbYellow, GameConstants.ArgbBlack));
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

            _factory.CreateMonster(
                randomPosition,
                new CoreGlyph((char)glyphCode, foregroundArgb, GameConstants.ArgbBlack),
                new Health { Current = GameConstants.DefaultMonsterHealth, Max = GameConstants.DefaultMonsterHealth },
                glyphCode == 'D' ? GameConstants.DragonAttack : GameConstants.DefaultMonsterAttack,
                EntityFactory.InferBehavior((char)glyphCode),
                gainPerTurn,
                actionCost);

            break;
        }
    }
}
