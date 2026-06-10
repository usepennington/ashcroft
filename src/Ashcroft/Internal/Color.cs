using SkiaSharp;

namespace Ashcroft.Internal;

/// <summary>
/// Parses the hex color strings accepted everywhere in the public API, so casual users
/// never construct an <see cref="SKColor"/> by hand. Supported forms (with or without a
/// leading <c>#</c>): <c>rgb</c>, <c>rrggbb</c>, <c>aarrggbb</c>.
/// </summary>
internal static class Color
{
    public static SKColor Parse(string hex)
    {
        if (TryParse(hex, out var color))
            return color;
        throw new FormatException($"'{hex}' is not a valid hex color. Expected #rgb, #rrggbb, or #aarrggbb.");
    }

    public static bool TryParse(string? hex, out SKColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        ReadOnlySpan<char> s = hex.AsSpan().Trim();
        if (s[0] == '#')
            s = s[1..];

        switch (s.Length)
        {
            case 3: // rgb -> expand each nibble (f -> ff)
                if (!Nib(s[0], out var r3) || !Nib(s[1], out var g3) || !Nib(s[2], out var b3))
                    return false;
                color = new SKColor((byte)(r3 * 17), (byte)(g3 * 17), (byte)(b3 * 17));
                return true;

            case 6: // rrggbb
                if (!Byte2(s[0], s[1], out var r6) || !Byte2(s[2], s[3], out var g6) || !Byte2(s[4], s[5], out var b6))
                    return false;
                color = new SKColor(r6, g6, b6);
                return true;

            case 8: // aarrggbb (alpha first)
                if (!Byte2(s[0], s[1], out var a8) || !Byte2(s[2], s[3], out var r8) ||
                    !Byte2(s[4], s[5], out var g8) || !Byte2(s[6], s[7], out var b8))
                    return false;
                color = new SKColor(r8, g8, b8, a8);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Returns the color with its alpha multiplied by <paramref name="factor"/> (0–1, clamped).</summary>
    public static SKColor WithOpacity(SKColor color, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        return color.WithAlpha((byte)Math.Round(color.Alpha * factor));
    }

    private static bool Nib(char c, out int value)
    {
        if (c is >= '0' and <= '9') { value = c - '0'; return true; }
        if (c is >= 'a' and <= 'f') { value = c - 'a' + 10; return true; }
        if (c is >= 'A' and <= 'F') { value = c - 'A' + 10; return true; }
        value = 0;
        return false;
    }

    private static bool Byte2(char hi, char lo, out byte value)
    {
        value = 0;
        if (!Nib(hi, out var h) || !Nib(lo, out var l))
            return false;
        value = (byte)(h * 16 + l);
        return true;
    }
}
