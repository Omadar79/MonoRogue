/// <summary>
/// Simple, UI-agnostic DTOs (Data Transfer Objects) for map / entity / player persistence and transfer.
/// These live in MonoRogue.Core so core systems can operate with plain data without depending on SadConsole types.
/// </summary>


namespace MonoRogue.Core;
// 
public sealed record GlyphDTO(char Glyph, int ForegroundArgb, int BackgroundArgb);

public sealed record EntityDTO(int X, int Y, GlyphDTO Glyph, bool BlocksMovement, bool IsPlayer, int SavedEntityId = 0);

public sealed record EffectDTO(
	int TargetSavedEntityId,
	EffectKind Kind,
	int RemainingTime,
	int TickInterval,
	int TimeUntilNextTick,
	int Magnitude);

public sealed record MapData(int Width, int Height, List<EntityDTO> Entities, List<EffectDTO>? Effects = null, int Version = 2);

public sealed record PlayerState(int X, int Y, GlyphDTO Glyph /* add stats/inventory fields here as needed */);

