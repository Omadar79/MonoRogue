using SadConsole;

namespace MonoRogue.Core;
/// <summary>
/// Maps between UI-specific <see cref="ColoredGlyph"/> and the UI-agnostic <see cref="GlyphDTO"/>.
/// Implement this in the UI project (SadConsole-based) to control how DTO colors/glyphs are turned
/// into runtime ColoredGlyphs and vice versa.
/// </summary>
public interface IGlyphMapper
{
    ColoredGlyph ToColoredGlyph(GlyphDTO glyphDTO);
    GlyphDTO ToGlyphDTO(ColoredGlyph coloredGlyph);
}

