using System.Reflection;
using Color = SadRogue.Primitives.Color;

namespace MonoRogue.UI;

public static class ColorConverter
{
    // Convert SadRogue.Primitives.Color to ARGB int using reflection-based fallbacks.
    public static int ToArgb(Color color)
    {
        var typeColor = typeof(Color);

        // Try common property names that may exist in different versions.
        var packedProp = typeColor.GetProperty("PackedValue", BindingFlags.Public | BindingFlags.Instance);
        if (packedProp != null)
        {
            var val = packedProp.GetValue(color);
            switch (val)
            {
                case uint u:
                    return unchecked((int)u);

                case int i:
                    return i;

                case long l:
                    return unchecked((int)l);

                default:
                    return 0;
            }
        }

        // Try R,G,B,A properties (could be byte, int, float).
        var propR = typeColor.GetProperty("R");
        var propG = typeColor.GetProperty("G");
        var propB = typeColor.GetProperty("B");
        var propA = typeColor.GetProperty("A");
        if (propR != null && propG != null && propB != null)
        {
            try
            {
                var rv = propR.GetValue(color);
                var gv = propG.GetValue(color);
                var bv = propB.GetValue(color);
                var av = propA?.GetValue(color) ?? 255;

                int r = ToByteScaled(rv);
                int g = ToByteScaled(gv);
                int b = ToByteScaled(bv);
                int a = ToByteScaled(av);

                return (a << 24) | (r << 16) | (g << 8) | b;
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // Try ToArgb method.
        var toArgb = typeColor.GetMethod("ToArgb", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (toArgb != null)
        {
            var val = toArgb.Invoke(color, Array.Empty<object>());
            if (val is int ii)
            {
                return ii;
            }
        }

        return 0;
    }

    private static int ToByteScaled(object? val)
    {
        switch (val)
        {
            case null:
                return 0;

            case byte b:
                return b;

            case int i:
                return Math.Clamp(i, 0, 255);

            case float f:
                return (int)Math.Clamp(Math.Round(f * 255f), 0, 255);

            case double d:
                return (int)Math.Clamp(Math.Round(d * 255.0), 0, 255);

            case long l:
                return (int)Math.Clamp(l, 0, 255);

            default:
                return 0;
        }
    }

    // Convert ARGB int back to SadRogue.Primitives.Color using reflection to find a suitable ctor or factory method.
    public static Color FromArgb(int argb)
    {
        var typeColor = typeof(Color);
        int a = (argb >> 24) & 0xFF;
        int r = (argb >> 16) & 0xFF;
        int g = (argb >> 8) & 0xFF;
        int b = argb & 0xFF;

        // Try constructors: Color(byte r, byte g, byte b) or Color(int r,int g,int b).
        var constructors = typeColor.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in constructors)
        {
            var pars = ctor.GetParameters();
            try
            {
                switch (pars.Length)
                {
                    case 3 when pars[0].ParameterType == typeof(byte):
                        return (Color)ctor.Invoke([(byte)r, (byte)g, (byte)b]);

                    case 3 when pars[0].ParameterType == typeof(int):
                        return (Color)ctor.Invoke([r, g, b]);

                    case 4 when pars[0].ParameterType == typeof(byte):
                        return (Color)ctor.Invoke([(byte)r, (byte)g, (byte)b, (byte)a]);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // Try static FromArgb method.
        var fromArgb = typeColor.GetMethod("FromArgb", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        if (fromArgb != null)
        {
            var res = fromArgb.Invoke(null, new object[] { argb });
            if (res is Color c) return c;
        }

        // As last resort, try implicit conversion from System.Drawing.Color if available.
        try
        {
            var sysColorType = Type.GetType("System.Drawing.Color");
            if (sysColorType != null)
            {
                var sysColor = System.Drawing.Color.FromArgb(a, r, g, b);
                // Try Color.FromArgb(int) again with packed int from System.Drawing.
                if (fromArgb != null)
                {
                    var res = fromArgb.Invoke(null, new object[] { sysColor.ToArgb() });
                    if (res is Color c2) return c2;
                }
            }
        }
        catch (Exception)
        {
            // ignored
        }

        // Fallback: return default white/black combination.
        return new Color(255, 255, 255);
    }
}
