using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Presentation;

/// <summary>
/// Names a diagnostic severity in words. The shell reports a condition in its
/// status area and announces it in a snackbar, and both have to name the severity
/// the same way without resting on its colour, so the wording lives in one place.
/// </summary>
internal static class SeverityText
{
    /// <summary>Names a severity in words.</summary>
    /// <param name="severity">The severity to name.</param>
    /// <returns>The word the shell shows for that severity.</returns>
    internal static string Of(DiagnosticSeverity severity)
        => severity switch
        {
            DiagnosticSeverity.Error => "Error",
            DiagnosticSeverity.Warning => "Warning",
            _ => "Information",
        };
}
