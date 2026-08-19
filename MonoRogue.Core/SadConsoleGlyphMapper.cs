using MonoRogue.Core;
using SadConsole;
using Color = SadRogue.Primitives.Color;

namespace MonoRogue.UI;
public class SadConsoleGlyphMapper : IGlyphMapper
{
    public ColoredGlyph ToColoredGlyph(GlyphDTO? glyphDTO)
    {
        if (glyphDTO == null)
        {
            return new ColoredGlyph(Color.White, Color.Black, (char)0);
        }

        var fg = ColorConverter.FromArgb(glyphDTO.ForegroundArgb);
        var bg = ColorConverter.FromArgb(glyphDTO.BackgroundArgb);
        return new ColoredGlyph(fg, bg, glyphDTO.Glyph);
    }

    public GlyphDTO ToGlyphDTO(ColoredGlyph coloredGlyph)
    {
        if (coloredGlyph.Equals(null))
        {
            return new GlyphDTO((char)0, 0, 0);
        }

        var ch = (char)coloredGlyph.Glyph;
        var fgArgb = ColorConverter.ToArgb(coloredGlyph.Foreground);
        var bgArgb = ColorConverter.ToArgb(coloredGlyph.Background);
        return new GlyphDTO(ch, fgArgb, bgArgb);
    }
}

