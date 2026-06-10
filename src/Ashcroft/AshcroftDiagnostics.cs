namespace Ashcroft;

/// <summary>
/// Lightweight diagnostics hook. Attach an <see cref="Action{String}"/> to observe
/// non-fatal events such as font-family fallback. Libraries shouldn't crash a build
/// pipeline over a missing font, but they should let you see what happened.
/// </summary>
public static class AshcroftDiagnostics
{
    /// <summary>Invoked with a human-readable message when a noteworthy fallback occurs.</summary>
    public static Action<string>? Log { get; set; }

    internal static void Report(string message) => Log?.Invoke(message);
}
