using Arch.Core;
using MonoRogue.Core.Systems;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Converts the ECS world and player inventory to/from plain DTOs (<see cref="MapData"/>) for persistence.
/// Reading is delegated to <see cref="WorldSnapshotReader"/> and entity construction to <see cref="EntityFactory"/>;
/// this class only performs the DTO mapping. File I/O is handled separately by <see cref="MapPersistenceHelpers"/>.
/// The live level is stored directly on <see cref="MapData"/>; other visited levels are stored as
/// <see cref="LevelDataDTO"/> snapshots and swapped in/out via <see cref="CaptureLevel"/>/<see cref="LoadLevel"/>.
/// </summary>
public sealed class MapSerializer
{
    private readonly EffectSystem _effects;
    private readonly Inventory _inventory;
    private readonly PlayerExperience _experience;
    private readonly EntityFactory _factory;
    private readonly WorldSnapshotReader _reader;
    private readonly SpatialMap _spatial;
    private readonly VisibilityMap _visibility;
    private readonly int _mapWidth;
    private readonly int _mapHeight;

    public MapSerializer(EffectSystem effects, Inventory inventory, PlayerExperience experience, EntityFactory factory, WorldSnapshotReader reader, SpatialMap spatial, VisibilityMap visibility, int mapWidth, int mapHeight)
    {
        _effects = effects;
        _inventory = inventory;
        _experience = experience;
        _factory = factory;
        _reader = reader;
        _spatial = spatial;
        _visibility = visibility;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
    }

    /// <summary>
    /// Saves the run: the live level (entities incl. the player, effects, terrain, exploration),
    /// the dungeon depth and run seed, and snapshots of every other visited level.
    /// </summary>
    public MapData Save(int depth, int seed, IReadOnlyDictionary<int, LevelDataDTO> levelCache)
    {
        var (entities, savedEntityIds, snapshotEffects) = BuildEntityDtos(includePlayer: true);
        var effects = BuildEffectDtos(savedEntityIds, snapshotEffects);

        var inventory = _inventory.GetStacks().Select(s => new ItemStackDTO(s.Name, s.Kind, s.Count, s.Magnitude)).ToList();

        var tiles = CaptureTiles();
        var levels = levelCache.Values.OrderBy(l => l.Depth).ToList();
        var visibility = _visibility.CaptureExplored();

        return new MapData(_mapWidth, _mapHeight, entities, effects, Version: CurrentSaveVersion, Inventory: inventory, PlayerExperience: _experience.GetCurrent(), Tiles: tiles, Depth: depth, Seed: seed, Levels: levels, Visibility: visibility);
    }

    /// <summary>The save format version written by <see cref="Save"/>. Older versions remain loadable.</summary>
    public const int CurrentSaveVersion = 6;

    /// <summary>
    /// Captures the currently loaded level (everything except the player) so it can be restored later.
    /// Effects targeting the player are excluded: the player carries them across levels.
    /// </summary>
    public LevelDataDTO CaptureLevel(int depth, Point playerPosition)
    {
        var (entities, savedEntityIds, snapshotEffects) = BuildEntityDtos(includePlayer: false);
        var effects = BuildEffectDtos(savedEntityIds, snapshotEffects);
        return new LevelDataDTO(depth, entities, effects, CaptureTiles(), _visibility.CaptureExplored(), playerPosition.X, playerPosition.Y);
    }

    /// <summary>
    /// Loads a full save. The live level is rebuilt in the world; depth, run seed and the cached
    /// (visited but not current) levels are returned for the caller to track.
    /// </summary>
    public void Load(MapData? mapData, out int depth, out int seed, out Dictionary<int, LevelDataDTO> levelCache)
    {
        depth = 1;
        seed = 0;
        levelCache = new Dictionary<int, LevelDataDTO>();

        if (mapData == null)
        {
            return;
        }

        depth = Math.Max(1, mapData.Depth);
        seed = mapData.Seed;
        if (mapData.Levels != null)
        {
            foreach (var level in mapData.Levels)
            {
                levelCache[level.Depth] = level;
            }
        }

        _factory.ClearWorld();
        RestoreTiles(mapData);

        var loadedEntityBySavedId = LoadEntities(mapData.Entities);
        LoadEffects(mapData.Effects, loadedEntityBySavedId);

        _inventory.Clear();
        if (mapData.Inventory != null)
        {
            foreach (var s in mapData.Inventory)
            {
                _inventory.AddStack(new ItemStack(s.Kind, s.Name, s.Count, s.Magnitude));
            }
        }

        _experience.SetExperience(mapData.PlayerExperience);
    }

    /// <summary>
    /// Restores a previously captured level into the world. The player entity is preserved
    /// (only non-player entities are cleared and rebuilt). Returns the saved arrival cell.
    /// </summary>
    public Point LoadLevel(LevelDataDTO level)
    {
        ClearNonPlayerEntities();
        RestoreTilesFromList(level.Tiles, _mapWidth, _mapHeight);

        var loadedEntityBySavedId = LoadEntities(level.Entities);
        LoadEffects(level.Effects, loadedEntityBySavedId);

        return new Point(level.PlayerX, level.PlayerY);
    }

    /// <summary>
    /// Destroys every entity except the player and the effects targeting the player, so a
    /// different level's entities can be loaded without recreating the player.
    /// </summary>
    public void ClearNonPlayerEntities()
    {
        var player = _reader.GetPlayerEntity();
        var keep = new HashSet<Entity>();
        if (player.HasValue)
        {
            keep.Add(player.Value);
        }

        var effectTargets = new Dictionary<Entity, Entity>();
        var effectQuery = new QueryDescription().WithAll<EffectTarget>();
        _reader.GetWorld().Query(in effectQuery, (Entity entity, ref EffectTarget target) =>
        {
            effectTargets[entity] = target.Value;
        });

        var allEntities = _reader.GetAllEntities();

        // Effects attached to a kept entity survive the level change with their target.
        foreach (var entity in allEntities)
        {
            if (effectTargets.TryGetValue(entity, out var target) && keep.Contains(target))
            {
                keep.Add(entity);
            }
        }

        foreach (var entity in allEntities)
        {
            if (!keep.Contains(entity))
            {
                _factory.Destroy(entity);
            }
        }
    }

    // ---- Entity/Effect DTO helpers ----

    private (List<EntityDTO> Entities, Dictionary<Entity, int> SavedEntityIds, List<SnapshotEffect> Effects) BuildEntityDtos(bool includePlayer)
    {
        var snapshot = _reader.Capture();

        var entities = new List<EntityDTO>();
        var savedEntityIds = new Dictionary<Entity, int>();
        var nextSavedEntityId = 1;

        foreach (var renderable in snapshot.Renderables)
        {
            var isPlayer = snapshot.PlayerEntities.Contains(renderable.Entity);
            if (!includePlayer && isPlayer)
            {
                continue;
            }

            var glyphDto = new GlyphDTO(renderable.Glyph.Glyph, renderable.Glyph.ForegroundArgb, renderable.Glyph.BackgroundArgb);

            var isBlocked = snapshot.BlockingPositions.Contains(renderable.Position);
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

            StairDirection? stairs = null;
            if (snapshot.Stairs.TryGetValue(renderable.Entity, out var stairDirection))
            {
                stairs = stairDirection;
            }

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
                experienceValue,
                stairs));
        }

        return (entities, savedEntityIds, snapshot.Effects);
    }

    private static List<EffectDTO> BuildEffectDtos(Dictionary<Entity, int> savedEntityIds, List<SnapshotEffect> snapshotEffects)
    {
        var effects = new List<EffectDTO>();
        foreach (var effect in snapshotEffects)
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

        return effects;
    }

    private Dictionary<int, Entity> LoadEntities(List<EntityDTO> entityDtos)
    {
        var loadedEntityBySavedId = new Dictionary<int, Entity>();

        foreach (var e in entityDtos)
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
            else if (e.Stairs is StairDirection stairDirection)
            {
                createdEntity = _factory.CreateStairs(position, glyph, stairDirection);
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

        return loadedEntityBySavedId;
    }

    private void LoadEffects(List<EffectDTO>? effectDtos, Dictionary<int, Entity> loadedEntityBySavedId)
    {
        if (effectDtos == null)
        {
            return;
        }

        foreach (var effect in effectDtos)
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

    // ---- Terrain helpers ----

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
        RestoreTilesFromList(mapData.Tiles, mapData.Width, mapData.Height);
    }

    private void RestoreTilesFromList(List<TileKind>? tiles, int savedWidth, int savedHeight)
    {
        if (tiles == null)
        {
            return;
        }

        var target = _spatial.GetTileMap();
        target.Fill(TileKind.Floor);

        for (int y = 0; y < target.GetHeight(); y++)
        {
            for (int x = 0; x < target.GetWidth(); x++)
            {
                if (x >= savedWidth || y >= savedHeight)
                {
                    continue;
                }

                var index = y * savedWidth + x;
                if (index < tiles.Count)
                {
                    target.SetTile(x, y, tiles[index]);
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
