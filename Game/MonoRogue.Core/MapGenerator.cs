using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Populates the world with entities: the player, monsters, items, and treasure. Reads content definitions from the 
/// <see cref="MonoRogue.Data"/> loaders and delegates entity construction to <see cref="EntityFactory"/>. Placement 
/// picks walkable floor cells via <see cref="SpatialMap.GetRandomWalkableCell(Random)"/> (items) and 
/// <see cref="SpatialMap.GetRandomOpenCell(Random)"/> (blocking entities), so nothing ever spawns inside a wall. 
/// The static terrain layout is produced separatelyby an <see cref="IDungeonLayoutGenerator"/> before this class runs;
/// the <see cref="Random"/> instance is injected so placement is reproducible when a fixed seed is supplied.
/// </summary>
public sealed class MapGenerator
{
    private readonly EntityFactory _factory;
    private readonly SpatialMap _spatial;
    private readonly Random _rng;

    public MapGenerator(EntityFactory factory, SpatialMap spatial, Random rng)
    {
        _factory = factory;
        _spatial = spatial;
        _rng = rng;
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
        var center = _spatial.GetCenter();
        
        // The player is placed first, before any other entities exist, so a walkable center is the deterministic default.
        // Fall back to a random open cell if a future layout generator ever carves a wall at the center.
        var spawn = _spatial.CanOccupy(center)
            ? center
            : (_spatial.GetRandomOpenCell(_rng) ?? center);

        _factory.CreatePlayer(
            spawn,
            new CoreGlyph('@', GameConstants.ArgbWhite, GameConstants.ArgbBlack),
            new Health { Current = GameConstants.DefaultPlayerHealth, Max = GameConstants.DefaultPlayerHealth },
            GameConstants.DefaultPlayerAttack);
    }

    private void CreateItem(ItemDefinition definition)
    {
        var position = _spatial.GetRandomWalkableCell(_rng);
        if (position is not Point p)
        {
            return;
        }

        _factory.CreateItem(
            p,
            new CoreGlyph(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb),
            definition.Kind,
            definition.Name,
            definition.Magnitude);
    }

    private void CreateMonster(MonsterDefinition definition)
    {
        var position = _spatial.GetRandomOpenCell(_rng);
        if (position is not Point p)
        {
            return;
        }

        _factory.CreateMonster(
            p,
            new CoreGlyph(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb),
            new Health { Current = GameConstants.DefaultMonsterHealth, Max = GameConstants.DefaultMonsterHealth },
            Math.Max(1, definition.Damage),
            new MonsterBehavior { Type = definition.Behavior, Range = definition.Range, SpecialEnergyCost = definition.SpecialEnergyCost },
            definition.GainPerTurn,
            definition.ActionCost,
            definition.Experience);
    }

    private void CreateTreasure()
    {
        var position = _spatial.GetRandomOpenCell(_rng);
        if (position is not Point p)
        {
            return;
        }

        _factory.CreateBlocker(p, new CoreGlyph('v', GameConstants.ArgbYellow, GameConstants.ArgbBlack));
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
        var position = _spatial.GetRandomOpenCell(_rng);
        if (position is not Point p)
        {
            return;
        }

        _factory.CreateMonster(
            p,
            new CoreGlyph((char)glyphCode, foregroundArgb, GameConstants.ArgbBlack),
            new Health
                {
                    Current = GameConstants.DefaultMonsterHealth
                    , Max = GameConstants.DefaultMonsterHealth
                },
            glyphCode == 'D' ? GameConstants.DragonAttack : GameConstants.DefaultMonsterAttack,
            EntityFactory.InferBehavior((char)glyphCode),
            gainPerTurn,
            actionCost,
            glyphCode == 'D' ? GameConstants.DragonExperience : GameConstants.DefaultMonsterExperience
            );
    }
}
