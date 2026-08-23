/// <summary>
/// Simple, UI-agnostic DTOs (Data Transfer Objects) for map / entity / player persistence and transfer.
/// These live in MonoRogue.Core so core systems can operate with plain data without depending on SadConsole types.
/// </summary>

using MonoRogue.Data;

namespace MonoRogue.Core;
// 
public sealed record GlyphDTO(char Glyph, int ForegroundArgb, int BackgroundArgb);

public sealed record EntityDTO(
    int X,
    int Y,
    GlyphDTO Glyph,
    bool BlocksMovement,
    bool IsPlayer,
    int SavedEntityId = 0,
    ItemKind? ItemKind = null,
    string? ItemName = null,
    int ItemMagnitude = 0,
    MonsterAIType? Behavior = null,
    int BehaviorRange = 0,
    int BehaviorSpecialEnergyCost = 0,
    int? Health = null,
    int? MaxHealth = null,
    int? Attack = null,
    int Experience = 0);

public sealed record EffectDTO(
	int TargetSavedEntityId,
	EffectKind Kind,
	int RemainingTime,
	int TickInterval,
	int TimeUntilNextTick,
	int Magnitude);

public sealed record ItemStackDTO(string Name, ItemKind Kind, int Count, int Magnitude);

public sealed record MapData(
    int Width,
    int Height,
    List<EntityDTO> Entities,
    List<EffectDTO>? Effects = null,
    int Version = 5,
    List<ItemStackDTO>? Inventory = null,
    int PlayerExperience = 0,
    List<TileKind>? Tiles = null);

public sealed record PlayerState(int X, int Y, GlyphDTO Glyph /* add stats/inventory fields here as needed */);

