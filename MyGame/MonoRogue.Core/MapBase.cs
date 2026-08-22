using Arch.Core;
using MonoRogue.Core.Systems;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

/// <summary>
/// Thin orchestrator for a single map/dungeon run. Owns the Arch <see cref="World"/>,
/// composes the focused systems, and exposes the public gameplay/persistence API.
/// Entity spawning, serialization, and inventory bookkeeping are delegated to
/// <see cref="MapGenerator"/>, <see cref="MapSerializer"/>, and <see cref="Inventory"/>.
/// </summary>
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
    private readonly MapGenerator _generator;
    private readonly MapSerializer _serializer;

    private readonly Inventory _inventory = new();

    // Queries MapBase still owns directly: render snapshots and item pickup.
    private readonly QueryDescription _renderableEntities;
    private readonly QueryDescription _itemEntities;

    public World World => _world;
    public int Width => _mapWidth;
    public int Height => _mapHeight;
    public IReadOnlyList<ItemStack> Inventory => _inventory.Stacks;

    public MapBase(int mapWidth, int mapHeight)
    {
        _world = World.Create();
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;

        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
        _itemEntities = new QueryDescription().WithAll<Position, Item>();

        _spatial = new SpatialMap(_world, mapWidth, mapHeight);
        _energy = new EnergySystem(_world);
        _effects = new EffectSystem(_world);
        _combat = new CombatSystem(_world, _effects);
        _playerAction = new PlayerActionSystem(_world, _spatial);
        _monsterAI = new MonsterAISystem(_world, _spatial, _combat);
        _generator = new MapGenerator(_world, _spatial, mapWidth, mapHeight);
        _serializer = new MapSerializer(_world, _effects, _inventory, mapWidth, mapHeight);

        _generator.CreateInitialPlayer();
        _generator.GenerateNewMap();
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    // ---- Persistence ----

    public MapData SaveMap() => _serializer.Save();

    public void LoadMap(MapData? mapData) => _serializer.Load(mapData);

    // Snapshot of every renderable entity (position + glyph) for the UI to draw.
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

        var (monsterActionsExecuted, effectResult, playerDied) = ResolveMonstersAndEffects(playerExists, playerEntity);

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

        var (monsterActionsExecuted, effectResult, playerDied) = ResolveMonstersAndEffects(playerExists, playerEntity);

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
        return _inventory.GetGold();
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
            _inventory.Add(item.Kind, item.Name, item.Magnitude);
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
        var index = _inventory.FindPotionIndex();
        if (index < 0)
        {
            return false;
        }

        if (!_playerAction.TryGetPlayerEntity(out var playerEntity))
        {
            return false;
        }

        var stack = _inventory.GetStack(index);
        if (!_combat.HealEntity(playerEntity, stack.Magnitude))
        {
            return false;
        }

        _inventory.ConsumeOne(index);
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

    // Shared tail for both turn types: clean up dead monsters, run monster AI, tick
    // effects, and check whether the player died. Previously ProcessPlayerTurn and
    // ProcessUsePotion diverged (only the former called DestroyDeadMonsters); this
    // unifies that behavior.
    private (int MonsterActionsExecuted, EffectTickResult Effects, bool PlayerDied) ResolveMonstersAndEffects(bool playerExists, Entity playerEntity)
    {
        _monsterAI.DestroyDeadMonsters();

        var monsterActionsExecuted = ProcessActors();
        var effectResult = _effects.ProcessEffects(GameConstants.TurnTimeQuantum, _combat.ApplyDamage);
        var playerDied = playerExists && _combat.IsEntityDead(playerEntity);

        return (monsterActionsExecuted, effectResult, playerDied);
    }

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
}
