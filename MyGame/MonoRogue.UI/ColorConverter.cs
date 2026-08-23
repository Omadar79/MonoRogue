using Color = SadRogue.Primitives.Color;

namespace MonoRogue.UI;

/// <summary>
/// Converts between the 0xAARRGGBB integer colors used by the core and JSON content
/// and the <see cref="Color"/> struct SadConsole renders with.
/// </summary>
public static class ColorConverter
{
    /// <summary>Builds a <see cref="Color"/> from a 0xAARRGGBB packed value.</summary>
    public static Color FromArgb(int argb) =>
        new Color(
            (byte)((argb >> 16) & 0xFF), // R
            (byte)((argb >> 8) & 0xFF),  // G
            (byte)(argb & 0xFF),         // B
            (byte)((argb >> 24) & 0xFF)); // A

    /// <summary>Packs a <see cref="Color"/> into a 0xAARRGGBB value.</summary>
    public static int ToArgb(Color color) =>
        (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
}