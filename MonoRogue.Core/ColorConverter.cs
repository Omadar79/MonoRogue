using System.Reflection;
using Color = SadRogue.Primitives.Color;


namespace MonoRogue.Core;
public static class ColorConverter
{
    // Convert SadRogue.Primitives.Color to ARGB int using reflection-based fallbacks.
    public static int ToArgb(Color color)
    {
        // Color is a struct in some versions; handle gracefully
        if (color == null) return 0;

        var t = typeof(Color);

        // Try common property names that may exist in different versions
        var packedProp = t.GetProperty("PackedValue", BindingFlags.Public | BindingFlags.Instance);
        if (packedProp != null)
        {
            var val = packedProp.GetValue(color);
            if (val is uint u) return unchecked((int)u);
            if (val is int i) return i;
            if (val is long l) return unchecked((int)l);
        }

        // Try R,G,B,A properties (could be byte, int, float)
        var propR = t.GetProperty("R");
        var propG = t.GetProperty("G");
        var propB = t.GetProperty("B");
        var propA = t.GetProperty("A");
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
            catch { }
        }

        // Try ToArgb method
        var toArgb = t.GetMethod("ToArgb", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (toArgb != null)
        {
            var val = toArgb.Invoke(color, Array.Empty<object>());
            if (val is int ii) return ii;
        }

        return 0;
    }

    private static int ToByteScaled(object? val)
    {
        if (val == null) return 0;
        if (val is byte b) return b;
        if (val is int i) return Math.Clamp(i, 0, 255);
        if (val is float f) return (int)Math.Clamp(Math.Round(f * 255f), 0, 255);
        if (val is double d) return (int)Math.Clamp(Math.Round(d * 255.0), 0, 255);
        if (val is long l) return (int)Math.Clamp(l, 0, 255);
        return 0;
    }

    // Convert ARGB int back to SadRogue.Primitives.Color using reflection to find a suitable ctor or factory method.
    public static Color FromArgb(int argb)
    {
        var t = typeof(Color);
        int a = (argb >> 24) & 0xFF;
        int r = (argb >> 16) & 0xFF;
        int g = (argb >> 8) & 0xFF;
        int b = argb & 0xFF;

        // Try constructors: Color(byte r, byte g, byte b) or Color(int r,int g,int b)
        var ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        foreach (var ctor in ctors)
        {
            var pars = ctor.GetParameters();
            try
            {
                if (pars.Length == 3 && pars[0].ParameterType == typeof(byte))
                {
                    return (Color)ctor.Invoke(new object[] { (byte)r, (byte)g, (byte)b });
                }
                if (pars.Length == 3 && pars[0].ParameterType == typeof(int))
                {
                    return (Color)ctor.Invoke(new object[] { r, g, b });
                }
                if (pars.Length == 4 && pars[0].ParameterType == typeof(byte))
                {
                    return (Color)ctor.Invoke(new object[] { (byte)r, (byte)g, (byte)b, (byte)a });
                }
            }
            catch { }
        }

        // Try static FromArgb method
        var fromArgb = t.GetMethod("FromArgb", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        if (fromArgb != null)
        {
            var res = fromArgb.Invoke(null, new object[] { argb });
            if (res is Color c) return c;
        }

        // As last resort, try implicit conversion from System.Drawing.Color if available
        try
        {
            var sysColorType = Type.GetType("System.Drawing.Color");
            if (sysColorType != null)
            {
                var sysColor = System.Drawing.Color.FromArgb(a, r, g, b);
                // Try Color.FromArgb(int) again with packed int from System.Drawing
                if (fromArgb != null)
                {
                    var res = fromArgb.Invoke(null, new object[] { sysColor.ToArgb() });
                    if (res is Color c2) return c2;
                }
            }
        }
        catch { }

        // Fallback: return default white/black combination
        return new Color(255, 255, 255);
    }
}

