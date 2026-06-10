namespace Ashcroft;

/// <summary>
/// A flat color or gradient fill. Construct one via the <see cref="Backgrounds"/> factory and pass it
/// to <c>CardBuilder.Background(BackgroundFill)</c>. The shader is built lazily at render time.
/// </summary>
public sealed class BackgroundFill
{
    internal enum FillKind { Solid, Linear, Radial }

    internal FillKind Kind { get; }
    internal string From { get; }
    internal string To { get; }
    internal float AngleDegrees { get; }

    internal BackgroundFill(FillKind kind, string from, string to, float angleDegrees)
    {
        Kind = kind;
        From = from;
        To = to;
        AngleDegrees = angleDegrees;
    }
}

/// <summary>Factory for built-in <see cref="BackgroundFill"/> values.</summary>
public static class Backgrounds
{
    /// <summary>A single solid color.</summary>
    public static BackgroundFill Solid(string color)
        => new(BackgroundFill.FillKind.Solid, color, color, 0);

    /// <summary>A linear gradient from <paramref name="from"/> to <paramref name="to"/> at the given angle.</summary>
    public static BackgroundFill LinearGradient(string from, string to, float angleDegrees = 135)
        => new(BackgroundFill.FillKind.Linear, from, to, angleDegrees);

    /// <summary>A radial gradient from <paramref name="center"/> outward to <paramref name="edge"/>.</summary>
    public static BackgroundFill RadialGradient(string center, string edge)
        => new(BackgroundFill.FillKind.Radial, center, edge, 0);
}
