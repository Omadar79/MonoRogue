using Arch.Core;
using MonoRogue.Core.Systems;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

public class MapBase : IDisposable
{
    private readonly World _world;

    private readonly int _mapWidth;
    private readonly int _mapHeight;

    private readonly SpatialMap _spatial;
    private readonly EnergySystem _energy;
    private readonly EffectSystem _effects;
    private readonly CombatSystem _combat;
    private readonly PlayerActionSystem _playerAction;
    private readonly MonsterAISystem _monsterAI;

    // Persistence-related queries. Turn systems own their own queries.
    private readonly QueryDescription _blockingEntities;
    private readonly QueryDescription _renderableEntities;
    private readonly QueryDescription _itemEntities;
    private readonly QueryDescription _effectEntities;
    private readonly QueryDescription _behaviorEntities;
    private readonly QueryDescription _healthEntities;
    private readonly QueryDescription _attackEntities;

    private readonly List<ItemStack> _inventory = new();

    public World World => _world;
    public int Width => _mapWidth;
    public int Height => _mapHeight;
    public IReadOnlyList<ItemStack> Inventory => _inventory;

    public MapBase(int mapWidth, int mapHeight)
    {
        _world = World.Create();
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;

        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
        _itemEntities = new QueryDescription().WithAll<Position, Item>();
        _effectEntities = new QueryDescription().WithAll<TimedEffect, EffectType, EffectTarget, EffectMagnitude>();
        _behaviorEntities = new QueryDescription().WithAll<MonsterBehavior>();
        _healthEntities = new QueryDescription().WithAll<Health>();
        _attackEntities = new QueryDescription().WithAll<Attack>();

        _spatial = new SpatialMap(_world, mapWidth, mapHeight);
        _energy = new EnergySystem(_world);
        _effects = new EffectSystem(_world);
        _combat = new CombatSystem(_world, _effects);
        _playerAction = new PlayerActionSystem(_world, _spatial);
        _monsterAI = new MonsterAISystem(_world, _spatial, _combat);

        CreateInitialPlayer();
        GenerateNewMap();
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    // ---- Persistence ----

    // Save current map data (entities + basic flags) into a UI-agnostic MapData DTO.
    public MapData SaveMap()
    {
        var entities = new List<EntityDTO>();
        var effects = new List<EffectDTO>();
        var savedEntityIds = new Dictionary<Entity, int>();
        var nextSavedEntityId = 1;

        var blockingPositions = new HashSet<Point>();
        _world.Query(in _blockingEntities, (ref Position pos) => { blockingPositions.Add(pos.Value); });

        var playerPositions = new HashSet<Point>();
        var playerQuery = new QueryDescription().WithAll<Position, ActorControlled>();
        _world.Query(in playerQuery, (ref Position pos, ref ActorControlled actor) => { if (actor.Kind == ActorKind.Player) playerPositions.Add(pos.Value); });

        var itemInfo = new Dictionary<Entity, (ItemKind Kind, string Name, int Magnitude)>();
        _world.Query(in _itemEntities, (Entity entity, ref Item item) =>
        {
            itemInfo[entity] = (item.Kind, item.Name, item.Magnitude);
        });

        var behaviorInfo = new Dictionary<Entity, MonsterBehavior>();
        _world.Query(in _behaviorEntities, (Entity entity, ref MonsterBehavior behavior) =>
        {
            behaviorInfo[entity] = behavior;
        });

        var healthInfo = new Dictionary<Entity, Health>();
        _world.Query(in _healthEntities, (Entity entity, ref Health health) =>
        {
            healthInfo[entity] = health;
        });

        var attackInfo = new Dictionary<Entity, int>();
        _world.Query(in _attackEntities, (Entity entity, ref Attack attack) =>
        {
            attackInfo[entity] = attack.Damage;
        });

        _world.Query(in _renderableEntities, (Entity entity, ref Position pos, ref RenderGlyph glyph) =>
        {
            var glyphDto = new GlyphDTO(glyph.Value.Glyph, glyph.Value.ForegroundArgb, glyph.Value.BackgroundArgb);

            var isBlocked = blockingPositions.Contains(pos.Value);
            var isPlayer = playerPositions.Contains(pos.Value);
            var savedEntityId = nextSavedEntityId++;
            savedEntityIds[entity] = savedEntityId;

            ItemKind? itemKind = null;
            string? itemName = null;
            int itemMagnitude = 0;
            if (itemInfo.TryGetValue(entity, out var info))
            {
                itemKind = info.Kind;
                itemName = info.Name;
                itemMagnitude = info.Magnitude;
            }

            MonsterAIType? behaviorType = null;
            int behaviorRange = 0;
            int behaviorSpecialEnergyCost = 0;
            if (behaviorInfo.TryGetValue(entity, out var behavior))
            {
                behaviorType = behavior.Type;
                behaviorRange = behavior.Range;
                behaviorSpecialEnergyCost = behavior.SpecialEnergyCost;
            }

            int? healthCurrent = null;
            int? healthMax = null;
            if (healthInfo.TryGetValue(entity, out var health))
            {
                healthCurrent = health.Current;
                healthMax = health.Max;
            }

            int? attackDamage = null;
            if (attackInfo.TryGetValue(entity, out var attack))
            {
                attackDamage = attack;
            }

            entities.Add(new EntityDTO(pos.Value.X, pos.Value.Y, glyphDto, isBlocked, isPlayer, savedEntityId, itemKind, itemName, itemMagnitude, behaviorType, behaviorRange, behaviorSpecialEnergyCost, healthCurrent, healthMax, attackDamage));
        });

        _world.Query(in _effectEntities, (ref TimedEffect timed, ref EffectType type, ref EffectTarget target, ref EffectMagnitude magnitude) =>
        {
            if (!savedEntityIds.TryGetValue(target.Value, out var targetSavedEntityId))
            {
                return;
            }

            effects.Add(new EffectDTO(
                targetSavedEntityId,
                type.Value,
                timed.RemainingTime,
                timed.TickInterval,
                timed.TimeUntilNextTick,
                magnitude.Value));
        });

        var inventory = _inventory.Select(s => new ItemStackDTO(s.Name, s.Kind, s.Count, s.Magnitude)).ToList();

        return new MapData(_mapWidth, _mapHeight, entities, effects, Inventory: inventory);
    }

    // Load map data into the current world. Clears and recreates entities from the DTO.
    public void LoadMap(MapData? mapData)
    {
        if (mapData == null)
        {
            return;
        }

        _world.Clear();

        var loadedEntityBySavedId = new Dictionary<int, Entity>();

        foreach (var e in mapData.Entities)
        {
            var pos = new Position(new Point(e.X, e.Y));
            var glyph = new RenderGlyph(new CoreGlyph(e.Glyph.Glyph, e.Glyph.ForegroundArgb, e.Glyph.BackgroundArgb));
            Entity createdEntity;

            if (e.IsPlayer)
            {
                createdEntity = _world.Create(
                    pos,
                    glyph,
                    new ActorControlled { Kind = ActorKind.Player },
                    new BlocksMovement(),
                    new Health { Current = e.Health ?? GameConstants.DefaultPlayerHealth, Max = e.MaxHealth ?? GameConstants.DefaultPlayerHealth },
                    new Attack { Damage = e.Attack ?? GameConstants.DefaultPlayerAttack },
                    new Energy { Current = GameConstants.DefaultActionCost, GainPerTurn = GameConstants.DefaultEnergyPerTurn, ActionCost = GameConstants.DefaultActionCost });
            }
            else if (e.BlocksMovement)
            {
                if (e.Glyph.Glyph is 'M' or 'g' or 'D')
                {
                    createdEntity = _world.Create(pos,
                        glyph,
                        new Health { Current = e.Health ?? GameConstants.DefaultMonsterHealth, Max = e.MaxHealth ?? GameConstants.DefaultMonsterHealth },
                        new Attack { Damage = e.Attack ?? (e.Glyph.Glyph == 'D' ? GameConstants.DragonAttack : GameConstants.DefaultMonsterAttack) },
                        new BlocksMovement(),
                        new ActorControlled { Kind = ActorKind.Monster },
                        ResolveBehavior(e),
                        new Energy { Current = 0, GainPerTurn = GameConstants.DefaultEnergyPerTurn, ActionCost = GameConstants.DefaultActionCost });
                }
                else
                {
                    createdEntity = _world.Create(pos, glyph, new BlocksMovement());
                }
            }
            else if (e.ItemName != null)
            {
                createdEntity = _world.Create(pos, glyph, new Item
                {
                    Kind = e.ItemKind ?? ItemKind.Gold,
                    Name = e.ItemName,
                    Magnitude = Math.Max(1, e.ItemMagnitude)
                });
            }
            else
            {
                createdEntity = _world.Create(pos, glyph);
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
                _inventory.Add(new ItemStack(s.Kind, s.Name, s.Count, s.Magnitude));
            }
        }

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

    // Snapshot of every renderable entity (position + glyph) for the UI to draw.
    // This is intentionally separate from SaveMap: rendering needs no save IDs,
    // item info, or effect payloads.
    public IReadOnlyList<RenderCell> GetRenderSnapshot()
    {
        var cells = new List<RenderCell>();
        _world.Query(in _renderableEntities, (ref Position pos, ref RenderGlyph glyph) =>
        {
            cells.Add(new RenderCell(pos.Value.X, pos.Value.Y, glyph.Value));
        });

        return cells;
    }

    // Extract a minimal PlayerState from the world (first found player entity).
    public PlayerState? ExtractPlayerState()
    {
        PlayerState? result = null;
        var q = new QueryDescription().WithAll<Position, ActorControlled, RenderGlyph>();
        _world.Query(in q, (ref Position pos, ref ActorControlled actor, ref RenderGlyph glyph) =>
        {
            if (actor.Kind != ActorKind.Player) return;
            var glyphDto = new GlyphDTO(glyph.Value.Glyph, glyph.Value.ForegroundArgb, glyph.Value.BackgroundArgb);
            result = new PlayerState(pos.Value.X, pos.Value.Y, glyphDto);
        });

        return result;
    }

    // ---- Turn orchestration ----

    // Process one gameplay turn in deterministic order: player action -> monster actions -> effects.
    public TurnResult ProcessPlayerTurn(Point playerDelta)
    {
        _energy.AdvanceActorEnergy();

        var playerMoved = false;
        var playerAttacked = false;
        var damageDealt = 0;
        var monsterKilled = false;
        var itemPickedUp = false;
        string? itemPickedUpName = null;

        var playerExists = _playerAction.TryGetPlayerEntity(out var playerEntity);
        var playerPosition = _playerAction.GetPlayerPosition();

        if (playerExists && playerPosition is Point pos)
        {
            if (playerDelta != Point.None)
            {
                var destination = pos + playerDelta;
                if (_monsterAI.TryGetMonsterEntityAt(destination, out var monsterEntity))
                {
                    // Bump-to-attack: spend an action to strike the monster instead of moving.
                    if (_energy.TryConsumePlayerEnergy())
                    {
                        damageDealt = _combat.ApplyDamage(monsterEntity, _combat.GetAttackDamage(playerEntity, GameConstants.DefaultPlayerAttack));
                        playerAttacked = damageDealt > 0;
                        monsterKilled = _combat.IsEntityDead(monsterEntity);
                    }
                }
                else
                {
                    playerMoved = _playerAction.TryMovePlayerNoRefresh(playerDelta);
                    if (playerMoved)
                    {
                        itemPickedUpName = TryPickupItemsAt(destination);
                        itemPickedUp = itemPickedUpName != null;
                    }
                }
            }
            else
            {
                _playerAction.TryMovePlayerNoRefresh(Point.None); // rest
            }
        }

        // Remove monsters the player killed so they do not act this turn.
        _monsterAI.DestroyDeadMonsters();

        var monsterActionsExecuted = ProcessActors();
        var effectResult = _effects.ProcessEffects(GameConstants.TurnTimeQuantum, _combat.ApplyDamage);

        var playerDied = playerExists && _combat.IsEntityDead(playerEntity);

        return new TurnResult(playerMoved, monsterActionsExecuted, effectResult.TicksProcessed, effectResult.EffectsExpired, playerAttacked, damageDealt, monsterKilled, playerDied, itemPickedUp, itemPickedUpName);
    }

    // Process a "use potion" action as a full turn: heal -> monster actions -> effects.
    public TurnResult ProcessUsePotion()
    {
        _energy.AdvanceActorEnergy();

        var playerExists = _playerAction.TryGetPlayerEntity(out var playerEntity);
        var potionUsed = false;
        var healAmount = 0;

        if (playerExists && _energy.TryConsumePlayerEnergy())
        {
            var before = GetPlayerHealth().Current;
            if (TryConsumePotion())
            {
                potionUsed = true;
                healAmount = GetPlayerHealth().Current - before;
            }
        }

        var monsterActionsExecuted = ProcessActors();
        var effectResult = _effects.ProcessEffects(GameConstants.TurnTimeQuantum, _combat.ApplyDamage);

        var playerDied = playerExists && _combat.IsEntityDead(playerEntity);

        return new TurnResult(false, monsterActionsExecuted, effectResult.TicksProcessed, effectResult.EffectsExpired, false, 0, false, playerDied, false, null, potionUsed, healAmount);
    }

    // ---- Player-facing helpers ----

    public bool TryMovePlayer(Point offset)
    {
        return _playerAction.TryMovePlayerNoRefresh(offset);
    }

    public bool TryRestPlayer()
    {
        return _playerAction.TryMovePlayerNoRefresh(Point.None);
    }

    public bool TryApplyPoisonToPlayer(int durationTime, int tickIntervalTime, int damagePerTick)
    {
        return TryApplyEffectToPlayer(EffectKind.Poison, durationTime, tickIntervalTime, damagePerTick);
    }

    public bool TryApplyProtectionToPlayer(int durationTime, int protectionAmount)
    {
        return TryApplyEffectToPlayer(EffectKind.Protection, durationTime, 0, protectionAmount);
    }

    public bool TryApplyLightToPlayer(int durationTime, int lightStrength)
    {
        return TryApplyEffectToPlayer(EffectKind.Light, durationTime, 0, lightStrength);
    }

    public bool TryApplyEffectToPlayer(EffectKind kind, int durationTime, int tickIntervalTime, int magnitude)
    {
        if (!_playerAction.TryGetPlayerEntity(out var playerEntity))
        {
            return false;
        }

        _effects.CreateEffect(playerEntity, kind, durationTime, tickIntervalTime, magnitude);
        return true;
    }

    // ---- Queries ----

    public int GetHealthAt(Point position)
    {
        return _combat.GetHealthAt(position);
    }

    public int GetActiveEffectCount()
    {
        return _effects.GetActiveEffectCount();
    }

    public (int Current, int Max) GetPlayerHealth()
    {
        if (!_playerAction.TryGetPlayerEntity(out var playerEntity))
        {
            return (0, 0);
        }

        return _combat.GetHealth(playerEntity);
    }

    public int GetGold()
    {
        return _inventory.Where(s => s.Kind == ItemKind.Gold).Sum(s => s.Count * s.Magnitude);
    }

    // ---- Inventory ----

    // Pick up all items at the given cell into the player's inventory. Returns the name of the first item picked, or null.
    public string? TryPickupItemsAt(Point position)
    {
        string? firstPickupName = null;
        var itemEntities = new List<Entity>();
        _world.Query(in _itemEntities, (Entity entity, ref Position pos, ref Item item) =>
        {
            if (pos.Value != position)
            {
                return;
            }

            firstPickupName ??= item.Name;
            AddItemToInventory(item.Kind, item.Name, item.Magnitude);
            itemEntities.Add(entity);
        });

        foreach (var entity in itemEntities)
        {
            _world.Destroy(entity);
        }

        return firstPickupName;
    }

    // Consume one potion from the inventory and heal the player. Returns false if no potion or healing had no effect.
    public bool TryConsumePotion()
    {
        var index = -1;
        for (var i = 0; i < _inventory.Count; i++)
        {
            if (_inventory[i].Kind == ItemKind.Potion && _inventory[i].Count > 0)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return false;
        }

        if (!_playerAction.TryGetPlayerEntity(out var playerEntity))
        {
            return false;
        }

        var stack = _inventory[index];
        if (!_combat.HealEntity(playerEntity, stack.Magnitude))
        {
            return false;
        }

        if (stack.Count <= 1)
        {
            _inventory.RemoveAt(index);
        }
        else
        {
            _inventory[index] = stack with { Count = stack.Count - 1 };
        }

        return true;
    }

    // ---- Map geometry ----

    public bool IsValidCell(Point position)
    {
        return _spatial.IsValidCell(position);
    }

    public Point GetMapCenter()
    {
        return _spatial.Center;
    }

    public bool IsBlocked(Point position)
    {
        return _spatial.IsBlocked(position);
    }

    // ---- Private helpers ----

    private int ProcessActors()
    {
        if (!_playerAction.TryGetPlayerEntity(out var playerEntity))
        {
            return 0;
        }

        var playerPosition = _playerAction.GetPlayerPosition();
        if (playerPosition is null)
        {
            return 0;
        }

        return _monsterAI.ProcessActors(playerPosition.Value, playerEntity);
    }

    private void AddItemToInventory(ItemKind kind, string name, int magnitude)
    {
        for (var i = 0; i < _inventory.Count; i++)
        {
            var stack = _inventory[i];
            if (stack.Kind == kind && stack.Name == name)
            {
                _inventory[i] = stack with { Count = stack.Count + 1 };
                return;
            }
        }

        _inventory.Add(new ItemStack(kind, name, 1, Math.Max(1, magnitude)));
    }

    // ---- Map generation ----

    private void GenerateNewMap()
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

    private void CreateInitialPlayer()
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
    private static MonsterBehavior InferBehavior(char glyph)
    {
        return glyph == 'D'
            ? new MonsterBehavior { Type = MonsterAIType.Breath, Range = 3, SpecialEnergyCost = 300 }
            : new MonsterBehavior { Type = MonsterAIType.Melee, Range = 1, SpecialEnergyCost = 0 };
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

        return InferBehavior(e.Glyph.Glyph);
    }
}
