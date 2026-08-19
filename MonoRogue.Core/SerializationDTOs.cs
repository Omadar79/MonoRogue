
namespace MonoRogue.Core;
// Simple, UI-agnostic DTOs (Data Transfer Objects) for map / entity / player persistence and transfer.
// These live in MonoRogue.Core so core systems can operate with plain data without depending on SadConsole types.

public sealed record GlyphDTO(char Glyph, int ForegroundArgb, int BackgroundArgb);

public sealed record EntityDTO(int X, int Y, GlyphDTO Glyph, bool BlocksMovement, bool IsPlayer);

public sealed record MapData(int Width, int Height, List<EntityDTO> Entities, int Version = 1);

public sealed record PlayerState(int X, int Y, GlyphDTO Glyph /* add stats/inventory fields here as needed */);

