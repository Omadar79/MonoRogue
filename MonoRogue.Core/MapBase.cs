using Arch.Core;
using MonoRogue.Data;
using SadRogue.Primitives;

namespace MonoRogue.Core;

public class MapBase : IDisposable
{
    private World _world;

    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private readonly QueryDescription _blockingEntities;
    private readonly QueryDescription _renderableEntities;
    private readonly QueryDescription _actorEntities;
    private readonly QueryDescription _effectEntities;
    private readonly QueryDescription _healthEntities;
    private readonly QueryDescription _playerEntities;
    private const int DefaultEnergyPerTurn = 100;
    private const int DefaultActionCost = 100;
    private const int MaxMonsterActionsPerTurn = 4;
    private const int TurnTimeQuantum = 100;
    private const int DefaultPlayerHealth = 20;
    private const int DefaultMonsterHealth = 8;
    private const int ArgbBlack = unchecked((int)0xFF000000);
    private const int ArgbWhite = unchecked((int)0xFFFFFFFF);
    private const int ArgbRed = unchecked((int)0xFFFF0000);
    private const int ArgbYellow = unchecked((int)0xFFFFFF00);
    private const int ArgbOrangeRed = unchecked((int)0xFFFF4500);

    public World World => _world;
    public int Width => _mapWidth;
    public int Height => _mapHeight;

    public MapBase(int mapWidth, int mapHeight)
    {
        _world = World.Create();
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _blockingEntities = new QueryDescription().WithAll<Position, BlocksMovement>();
        _renderableEntities = new QueryDescription().WithAll<Position, RenderGlyph>();
        _actorEntities = new QueryDescription().WithAll<Position, ActorControlled, Energy, RenderGlyph>();
        _effectEntities = new QueryDescription().WithAll<TimedEffect, EffectType, EffectTarget, EffectMagnitude>();
        _healthEntities = new QueryDescription().WithAll<Health>();
        _playerEntities = new QueryDescription().WithAll<ActorControlled>();

        CreateInitialPlayer();
        GenerateNewMap();
    }

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

        _world.Query(in _renderableEntities, (Entity entity, ref Position pos, ref RenderGlyph glyph) =>
        {
            var glyphDto = new GlyphDTO(glyph.Value.Glyph, glyph.Value.ForegroundArgb, glyph.Value.BackgroundArgb);

            var isBlocked = blockingPositions.Contains(pos.Value);
            var isPlayer = playerPositions.Contains(pos.Value);
            var savedEntityId = nextSavedEntityId++;
            savedEntityIds[entity] = savedEntityId;
            entities.Add(new EntityDTO(pos.Value.X, pos.Value.Y, glyphDto, isBlocked, isPlayer, savedEntityId));
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

        return new MapData(_mapWidth, _mapHeight, entities, effects);
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    // Load map data into the current world. This clears and recreates entities from the DTO.
    public void LoadMap(MapData? mapData)
    {
        if (mapData == null)
        {
            return;
        }

        _world.Dispose();
        _world = World.Create();

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
                    new Health { Current = DefaultPlayerHealth, Max = DefaultPlayerHealth },
                    new Energy { Current = DefaultActionCost, GainPerTurn = DefaultEnergyPerTurn, ActionCost = DefaultActionCost });
            }
            else if (e.BlocksMovement)
            {
                if (e.Glyph.Glyph is 'M' or 'g' or 'D')
                {
                    createdEntity = _world.Create(pos,
                        glyph,
                        new Health { Current = DefaultMonsterHealth, Max = DefaultMonsterHealth },
                        new BlocksMovement(),
                            new ActorControlled { Kind = ActorKind.Monster },
                        new Energy { Current = 0, GainPerTurn = DefaultEnergyPerTurn, ActionCost = DefaultActionCost });
                }
                else
                {
                    createdEntity = _world.Create(pos, glyph, new BlocksMovement());
                }
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

            CreateEffect(
                targetEntity,
                effect.Kind,
                effect.RemainingTime,
                effect.TickInterval,
                effect.Magnitude,
                effect.TimeUntilNextTick);
        }
    }

    // Extract a minimal PlayerState from the world (first found player entity)
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

    public bool TryMovePlayer(Point offset)
    {
        return TryMovePlayerNoRefresh(offset);
    }

    public bool TryRestPlayer()
    {
        return TryMovePlayerNoRefresh(Point.None);
    }

    // Process one gameplay turn in deterministic order: player action -> monster actions.
    public TurnResult ProcessPlayerTurn(Point playerDelta)
    {
        AdvanceActorEnergy();
        var playerMoved = TryMovePlayerNoRefresh(playerDelta);
        var monsterActionsExecuted = ProcessActors();
        var effectResult = ProcessEffects(TurnTimeQuantum);

        return new TurnResult(playerMoved, monsterActionsExecuted, effectResult.TicksProcessed, effectResult.EffectsExpired);
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
        if (!TryGetPlayerEntity(out var playerEntity))
        {
            return false;
        }

        CreateEffect(playerEntity, kind, durationTime, tickIntervalTime, magnitude);
        return true;
    }

    public int GetHealthAt(Point position)
    {
        int? health = null;
        _world.Query(in _healthEntities, (Entity entity, ref Health value) =>
        {
            if (health != null)
            {
                return;
            }

            if (TryGetPosition(entity, out var entityPosition) && entityPosition == position)
            {
                health = value.Current;
            }
        });

        return health ?? 0;
    }

    public int GetActiveEffectCount()
    {
        var count = 0;
        _world.Query(in _effectEntities, (ref TimedEffect timed, ref EffectType type, ref EffectTarget target, ref EffectMagnitude magnitude) =>
        {
            if (timed.RemainingTime > 0)
            {
                count++;
            }
        });

        return count;
    }

    public bool IsValidCell(Point position)
    {
        return position.X >= 0 && position.Y >= 0 && position.X < _mapWidth && position.Y < _mapHeight;
    }

    public Point GetMapCenter()
    {
        return new Point(_mapWidth / 2, _mapHeight / 2);
    }

    public bool IsBlocked(Point position)
    {
        var blocked = false;

        _world.Query(in _blockingEntities, (ref Position otherPosition) =>
        {
            if (otherPosition.Value == position)
            {
                blocked = true;
            }
        });

        return blocked;
    }

    private bool TryMovePlayerNoRefresh(Point offset)
    {
        var moved = false;
        var playerQuery = new QueryDescription().WithAll<Position, ActorControlled, Energy>();
        _world.Query(in playerQuery, (ref Position position, ref ActorControlled actor, ref Energy energy) =>
        {
            if (actor.Kind != ActorKind.Player) return;
            var actionCost = Math.Max(1, energy.ActionCost);
            if (energy.Current < actionCost)
            {
                return;
            }

            if (offset == Point.None)
            {
                energy.Current -= actionCost;
                return;
            }

            var destination = position.Value + offset;
            if (!IsValidCell(destination) || IsBlocked(destination))
            {
                return;
            }

            position.Value = destination;
            energy.Current -= actionCost;
            moved = true;
        });

        return moved;
    }

    private void AdvanceActorEnergy()
    {
        _world.Query(in _actorEntities, (ref ActorControlled actor, ref Energy energy) =>
        {
            if (energy.GainPerTurn <= 0)
            {
                return;
            }

            energy.Current += energy.GainPerTurn;
        });
    }

    private int ProcessActors()
    {
        var playerPosition = GetPlayerPosition();
        if (playerPosition is null)
        {
            return 0;
        }

        var actionsExecuted = 0;
        _world.Query(in _actorEntities, (ref Position monsterPosition, ref ActorControlled actor, ref Energy energy, ref RenderGlyph glyph) =>
        {
            if (actor.Kind != ActorKind.Monster) return;
            var actionsForThisMonster = 0;
            while (actionsForThisMonster < MaxMonsterActionsPerTurn)
            {
                var action = PlanMonsterAction(monsterPosition.Value, playerPosition.Value, glyph.Value, energy);
                if (action.EnergyCost <= 0 || energy.Current < action.EnergyCost)
                {
                    break;
                }

                if (ExecuteMonsterAction(ref monsterPosition, action, playerPosition.Value))
                {
                    actionsExecuted++;
                }

                energy.Current -= action.EnergyCost;
                actionsForThisMonster++;
            }
        });

        return actionsExecuted;
    }

    private MonsterActionPlan PlanMonsterAction(Point monsterPosition, Point playerPosition, CoreGlyph glyph, Energy energy)
    {
        var delta = playerPosition - monsterPosition;
        var stepX = Math.Sign(delta.X);
        var stepY = Math.Sign(delta.Y);
        var distance = Math.Abs(delta.X) + Math.Abs(delta.Y);

        var glyphChar = glyph.Glyph;
        if (glyphChar == 'D' && distance <= 3)
        {
            return new MonsterActionPlan(MonsterActionType.BreathAttack, Point.None, 300);
        }

        if (stepX != 0)
        {
            return new MonsterActionPlan(MonsterActionType.StepTowardPlayer, new Point(stepX, 0), Math.Max(1, energy.ActionCost));
        }

        if (stepY != 0)
        {
            return new MonsterActionPlan(MonsterActionType.StepTowardPlayer, new Point(0, stepY), Math.Max(1, energy.ActionCost));
        }

        return new MonsterActionPlan(MonsterActionType.Wait, Point.None, Math.Max(1, energy.ActionCost));
    }

    private bool ExecuteMonsterAction(ref Position monsterPosition, MonsterActionPlan action, Point playerPosition)
    {
        switch (action.Type)
        {
            case MonsterActionType.StepTowardPlayer:
            {
                var candidate = monsterPosition.Value + action.Delta;
                if (CanMonsterOccupy(candidate, monsterPosition.Value))
                {
                    monsterPosition.Value = candidate;
                    return true;
                }

                return false;
            }
            case MonsterActionType.BreathAttack:
                // Stub: combat/resistance pipeline will be added later.
                return IsInBreathRange(monsterPosition.Value, playerPosition, 3);
            case MonsterActionType.Wait:
            default:
                return false;
        }
    }

    private static bool IsInBreathRange(Point origin, Point target, int range)
    {
        var distance = Math.Abs(origin.X - target.X) + Math.Abs(origin.Y - target.Y);
        return distance <= range;
    }

    private Point? GetPlayerPosition()
    {
        Point? result = null;
        var playerQuery = new QueryDescription().WithAll<Position, ActorControlled>();
        _world.Query(in playerQuery, (ref Position position, ref ActorControlled actor) =>
        {
            if (actor.Kind != ActorKind.Player) return;
            result ??= position.Value;
        });

        return result;
    }

    private bool CanMonsterOccupy(Point destination, Point currentPosition)
    {
        if (!IsValidCell(destination) || destination == currentPosition)
        {
            return false;
        }

        var blocked = false;
        _world.Query(in _blockingEntities, (ref Position otherPosition) =>
        {
            if (otherPosition.Value == destination)
            {
                blocked = true;
            }
        });

        return !blocked;
    }

    private EffectTickResult ProcessEffects(int elapsedTime)
    {
        if (elapsedTime <= 0)
        {
            return new EffectTickResult(0, 0);
        }

        var ticksProcessed = 0;
        var expired = new List<Entity>();

        _world.Query(in _effectEntities, (Entity effectEntity, ref TimedEffect timed, ref EffectType effectType, ref EffectTarget target, ref EffectMagnitude magnitude) =>
        {
            timed.RemainingTime -= elapsedTime;

            if (timed.TickInterval > 0)
            {
                timed.TimeUntilNextTick -= elapsedTime;
                while (timed.RemainingTime > 0 && timed.TimeUntilNextTick <= 0)
                {
                    if (ApplyEffectTick(effectType.Value, target.Value, magnitude.Value))
                    {
                        ticksProcessed++;
                    }

                    timed.TimeUntilNextTick += timed.TickInterval;
                }
            }

            if (timed.RemainingTime <= 0)
            {
                expired.Add(effectEntity);
            }
        });

        foreach (var effectEntity in expired)
        {
            _world.Destroy(effectEntity);
        }

        return new EffectTickResult(ticksProcessed, expired.Count);
    }

    private bool ApplyEffectTick(EffectKind kind, Entity target, int magnitude)
    {
        switch (kind)
        {
            case EffectKind.Poison:
                return ApplyDamage(target, magnitude);
            case EffectKind.Light:
            case EffectKind.Protection:
            default:
                return false;
        }
    }

    private bool ApplyDamage(Entity target, int rawDamage)
    {
        var damage = Math.Max(0, rawDamage - GetActiveProtection(target));
        if (damage <= 0)
        {
            return false;
        }

        var applied = false;
        _world.Query(in _healthEntities, (Entity entity, ref Health health) =>
        {
            if (entity != target)
            {
                return;
            }

            health.Current = Math.Max(0, health.Current - damage);
            applied = true;
        });

        return applied;
    }

    private int GetActiveProtection(Entity target)
    {
        var protection = 0;
        _world.Query(in _effectEntities, (ref TimedEffect timed, ref EffectType effectType, ref EffectTarget effectTarget, ref EffectMagnitude magnitude) =>
        {
            if (timed.RemainingTime <= 0)
            {
                return;
            }

            if (effectType.Value != EffectKind.Protection || effectTarget.Value != target)
            {
                return;
            }

            protection += Math.Max(0, magnitude.Value);
        });

        return protection;
    }

    private void CreateEffect(Entity target, EffectKind kind, int durationTime, int tickIntervalTime, int magnitude, int? timeUntilNextTickOverride = null)
    {
        var safeDuration = Math.Max(1, durationTime);
        var safeInterval = Math.Max(0, tickIntervalTime);
        var safeTimeUntilNextTick = safeInterval;
        if (safeInterval > 0 && timeUntilNextTickOverride.HasValue)
        {
            safeTimeUntilNextTick = Math.Max(1, timeUntilNextTickOverride.Value);
        }
        else if (safeInterval == 0)
        {
            safeTimeUntilNextTick = 0;
        }

        _world.Create(
            new TimedEffect
            {
                RemainingTime = safeDuration,
                TickInterval = safeInterval,
                TimeUntilNextTick = safeTimeUntilNextTick
            },
            new EffectType { Value = kind },
            new EffectTarget { Value = target },
            new EffectMagnitude { Value = Math.Max(0, magnitude) });
    }

    private bool TryGetPlayerEntity(out Entity playerEntity)
    {
        var result = default(Entity);
        var found = false;
        _world.Query(in _playerEntities, (Entity entity, ref ActorControlled actor) =>
        {
            if (found || actor.Kind != ActorKind.Player)
            {
                return;
            }

            result = entity;
            found = true;
        });

        playerEntity = result;
        return found;
    }

    private bool TryGetPosition(Entity entity, out Point position)
    {
        var result = Point.None;
        var found = false;
        _world.Query(in _renderableEntities, (Entity candidate, ref Position value, ref RenderGlyph glyph) =>
        {
            if (found || candidate != entity)
            {
                return;
            }

            result = value.Value;
            found = true;
        });

        position = result;
        return found;
    }

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

            if (IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb));

            break;
        }
    }

    private void CreateMonster(MonsterDefinition definition)
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb(definition.Glyph, definition.ForegroundArgb, definition.BackgroundArgb),
                new Health { Current = DefaultMonsterHealth, Max = DefaultMonsterHealth },
                new BlocksMovement(),
                new ActorControlled { Kind = ActorKind.Monster },
                new Energy
                {
                    Current = 0,
                    GainPerTurn = Math.Max(1, definition.GainPerTurn),
                    ActionCost = Math.Max(1, definition.ActionCost)
                });

            break;
        }
    }

    // Create a player entity in the current world using a PlayerState DTO.
    private void CreatePlayerFromState(PlayerState? state)
    {
        if (state == null)
        {
            return;
        }

        var pos = new Position(new Point(state.X, state.Y));
        var glyph = new RenderGlyph(new CoreGlyph(state.Glyph.Glyph, state.Glyph.ForegroundArgb, state.Glyph.BackgroundArgb));

        _world.Create(pos,
            glyph,
            new ActorControlled { Kind = ActorKind.Player },
            new BlocksMovement(),
            new Health { Current = DefaultPlayerHealth, Max = DefaultPlayerHealth },
            new Energy { Current = DefaultActionCost, GainPerTurn = DefaultEnergyPerTurn, ActionCost = DefaultActionCost });
    }

    // Clear the current world. If preservePlayerState is true, player will be extracted and recreated after the world is cleared.
    private void ClearWorld(bool preservePlayerState = false)
    {
        PlayerState? saved = null;
        if (preservePlayerState)
        {
            saved = ExtractPlayerState();
        }

        _world.Dispose();
        _world = World.Create();

        if (saved != null)
        {
            CreatePlayerFromState(saved);
        }
    }

    private void CreateInitialPlayer()
    {
        var center = new Point(_mapWidth / 2, _mapHeight / 2);
        _world.Create(new Position(center),
            RenderGlyph.FromArgb('@', ArgbWhite, ArgbBlack),
            new ActorControlled { Kind = ActorKind.Player },
            new BlocksMovement(),
            new Health { Current = DefaultPlayerHealth, Max = DefaultPlayerHealth },
            new Energy { Current = DefaultActionCost, GainPerTurn = DefaultEnergyPerTurn, ActionCost = DefaultActionCost });
    }

    private void CreateTreasure()
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition), RenderGlyph.FromArgb('v', ArgbYellow, ArgbBlack), new BlocksMovement());
            break;
        }
    }

    private void CreateGoblin()
    {
        CreateMonster('g', ArgbRed, DefaultEnergyPerTurn, DefaultActionCost);
    }

    private void CreateDragon()
    {
        // Dragon gains energy like a normal actor but can spend more on special actions.
        CreateMonster('D', ArgbOrangeRed, DefaultEnergyPerTurn, DefaultActionCost);
    }

    private void CreateMonster(int glyphCode, int foregroundArgb, int gainPerTurn, int actionCost)
    {
        for (int i = 0; i < 1000; i++)
        {
            var randomPosition = new Point(Random.Shared.Next(0, _mapWidth), Random.Shared.Next(0, _mapHeight));

            if (IsBlocked(randomPosition))
            {
                continue;
            }

            _world.Create(new Position(randomPosition),
                RenderGlyph.FromArgb((char)glyphCode, foregroundArgb, ArgbBlack),
                new Health { Current = DefaultMonsterHealth, Max = DefaultMonsterHealth },
                new BlocksMovement(),
                new ActorControlled { Kind = ActorKind.Monster },
                new Energy
                {
                    Current = 0,
                    GainPerTurn = Math.Max(1, gainPerTurn),
                    ActionCost = Math.Max(1, actionCost)
                });

            break;
        }
    }
}
