using Arch.Core;
using MonoRogue.Core.Systems;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Converts the ECS world and player inventory to/from plain DTOs (<see cref="MapData"/>) for persistence.
/// Reading is delegated to <see cref="WorldSnapshotReader"/> and entity construction to <see cref="EntityFactory"/>;
/// this class only performs the DTO mapping. File I/O is handled separately by <see cref="MapPersistenceHelpers"/>.
/// </summary>
public sealed class MapSerializer
{
    private readonly EffectSystem _effects;
    private readonly Inventory _inventory;
    private readonly PlayerExperience _experience;
    private readonly EntityFactory _factory;
    private readonly WorldSnapshotReader _reader;
    private readonly SpatialMap _spatial;
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    public MapSerializer(EffectSystem effects, Inventory inventory, PlayerExperience experience, EntityFactory factory, WorldSnapshotReader reader, SpatialMap spatial, int mapWidth, int mapHeight)
    {
        _effects = effects;
        _inventory = inventory;
        _experience = experience;
        _factory = factory;
        _reader = reader;
        _spatial = spatial;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
    }

    public MapData Save()
    {
        var snapshot = _reader.Capture();

        var entities = new List<EntityDTO>();
        var effects = new List<EffectDTO>();
        var savedEntityIds = new Dictionary<Entity, int>();
        var nextSavedEntityId = 1;

        foreach (var renderable in snapshot.Renderables)
        {
            var glyphDto = new GlyphDTO(renderable.Glyph.Glyph, renderable.Glyph.ForegroundArgb, renderable.Glyph.BackgroundArgb);

            var isBlocked = snapshot.BlockingPositions.Contains(renderable.Position);
            var isPlayer = snapshot.PlayerPositions.Contains(renderable.Position);
            var savedEntityId = nextSavedEntityId++;
            savedEntityIds[renderable.Entity] = savedEntityId;

            ItemKind? itemKind = null;
            string? itemName = null;
            int itemMagnitude = 0;
            if (snapshot.Items.TryGetValue(renderable.Entity, out var item))
            {
                itemKind = item.Kind;
                itemName = item.Name;
                itemMagnitude = item.Magnitude;
            }

            MonsterAIType? behaviorType = null;
            int behaviorRange = 0;
            int behaviorSpecialEnergyCost = 0;
            if (snapshot.Behaviors.TryGetValue(renderable.Entity, out var behavior))
            {
                behaviorType = behavior.Type;
                behaviorRange = behavior.Range;
                behaviorSpecialEnergyCost = behavior.SpecialEnergyCost;
            }

            int? healthCurrent = null;
            int? healthMax = null;
            if (snapshot.Health.TryGetValue(renderable.Entity, out var health))
            {
                healthCurrent = health.Current;
                healthMax = health.Max;
            }

            int? attackDamage = null;
            if (snapshot.Attack.TryGetValue(renderable.Entity, out var attack))
            {
                attackDamage = attack;
            }

            snapshot.Experience.TryGetValue(renderable.Entity, out var experienceValue);

            entities.Add(new EntityDTO(
                renderable.Position.X,
                renderable.Position.Y,
                glyphDto,
                isBlocked,
                isPlayer,
                savedEntityId,
                itemKind,
                itemName,
                itemMagnitude,
                behaviorType,
                behaviorRange,
                behaviorSpecialEnergyCost,
                healthCurrent,
                healthMax,
                attackDamage,
                experienceValue));
        }

        foreach (var effect in snapshot.Effects)
        {
            if (!savedEntityIds.TryGetValue(effect.Target, out var targetSavedEntityId))
            {
                continue;
            }

            effects.Add(new EffectDTO(
                targetSavedEntityId,
                effect.Kind,
                effect.Timed.RemainingTime,
                effect.Timed.TickInterval,
                effect.Timed.TimeUntilNextTick,
                effect.Magnitude));
        }

        var inventory = _inventory.GetStacks().Select(s => new ItemStackDTO(s.Name, s.Kind, s.Count, s.Magnitude)).ToList();

        var tiles = CaptureTiles();

        return new MapData(_mapWidth, _mapHeight, entities, effects, Inventory: inventory, PlayerExperience: _experience.GetCurrent(), Tiles: tiles);
    }

    public void Load(MapData? mapData)
    {
        if (mapData == null)
        {
            return;
        }

        _factory.ClearWorld();
        RestoreTiles(mapData);

        var loadedEntityBySavedId = new Dictionary<int, Entity>();

        foreach (var e in mapData.Entities)
        {
            var position = new Point(e.X, e.Y);
            var glyph = new CoreGlyph(e.Glyph.Glyph, e.Glyph.ForegroundArgb, e.Glyph.BackgroundArgb);
            Entity createdEntity;

            if (e.IsPlayer)
            {
                createdEntity = _factory.CreatePlayer(
                    position,
                    glyph,
                    new Health { Current = e.Health ?? GameConstants.DefaultPlayerHealth, Max = e.MaxHealth ?? GameConstants.DefaultPlayerHealth },
                    e.Attack ?? GameConstants.DefaultPlayerAttack);
            }
            else if (e.BlocksMovement)
            {
                if (e.Glyph.Glyph is 'M' or 'g' or 'D')
                {
                    createdEntity = _factory.CreateMonster(
                        position,
                        glyph,
                        new Health { Current = e.Health ?? GameConstants.DefaultMonsterHealth, Max = e.MaxHealth ?? GameConstants.DefaultMonsterHealth },
                        e.Attack ?? (e.Glyph.Glyph == 'D' ? GameConstants.DragonAttack : GameConstants.DefaultMonsterAttack),
                        ResolveBehavior(e),
                        GameConstants.DefaultEnergyPerTurn,
                        GameConstants.DefaultActionCost,
                        e.Experience);
                }
                else
                {
                    createdEntity = _factory.CreateBlocker(position, glyph);
                }
            }
            else if (e.ItemName != null)
            {
                createdEntity = _factory.CreateItem(
                    position,
                    glyph,
                    e.ItemKind ?? ItemKind.Gold,
                    e.ItemName,
                    e.ItemMagnitude);
            }
            else
            {
                createdEntity = _factory.CreateDecoration(position, glyph);
            }

            if (e.SavedEntityId > 0)
            {
                loadedEntityBySavedId[e.SavedEntityId] = createdEntity;
            }
        }

        _inventory.Clear();
        if (mapData.Inventory != null)
        {
            foreach (var s in mapData.Inventory)
            {
                _inventory.AddStack(new ItemStack(s.Kind, s.Name, s.Count, s.Magnitude));
            }
        }

        _experience.SetExperience(mapData.PlayerExperience);

        if (mapData.Effects == null)
        {
            return;
        }

        foreach (var effect in mapData.Effects)
        {
            if (!loadedEntityBySavedId.TryGetValue(effect.TargetSavedEntityId, out var targetEntity))
            {
                continue;
            }

            _effects.CreateEffect(
                targetEntity,
                effect.Kind,
                effect.RemainingTime,
                effect.TickInterval,
                effect.Magnitude,
                effect.TimeUntilNextTick);
        }
    }

    // Flatten the static terrain grid into a row-major list of TileKind values.
    private List<TileKind> CaptureTiles()
    {
        var tiles = _spatial.GetTileMap();
        var data = new List<TileKind>(tiles.GetWidth() * tiles.GetHeight());
        for (int y = 0; y < tiles.GetHeight(); y++)
        {
            for (int x = 0; x < tiles.GetWidth(); x++)
            {
                data.Add(tiles.GetTile(x, y));
            }
        }

        return data;
    }

    // Rebuild the static terrain from the save. Legacy saves (version < 5) have no Tiles
    // field, so we leave the freshly generated layout untouched to preserve compatibility.
    private void RestoreTiles(MapData mapData)
    {
        if (mapData.Tiles == null)
        {
            return;
        }

        var tiles = _spatial.GetTileMap();
        tiles.Fill(TileKind.Floor);

        var savedWidth = mapData.Width;
        var savedHeight = mapData.Height;
        for (int y = 0; y < tiles.GetHeight(); y++)
        {
            for (int x = 0; x < tiles.GetWidth(); x++)
            {
                if (x >= savedWidth || y >= savedHeight)
                {
                    continue;
                }

                var index = y * savedWidth + x;
                if (index < mapData.Tiles.Count)
                {
                    tiles.SetTile(x, y, mapData.Tiles[index]);
                }
            }
        }
    }

    // Restore a monster's behavior from the save DTO, falling back to glyph inference
    // for legacy saves that predate behavior persistence.
    private static MonsterBehavior ResolveBehavior(EntityDTO e)
    {
        if (e.Behavior is MonsterAIType type)
        {
            return new MonsterBehavior
            {
                Type = type,
                Range = Math.Max(1, e.BehaviorRange),
                SpecialEnergyCost = Math.Max(0, e.BehaviorSpecialEnergyCost)
            };
        }

        return EntityFactory.InferBehavior(e.Glyph.Glyph);
    }
}
