using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Presentation;

/// <summary>
/// Words a diagnostic once, so the status area, a canvas tooltip, and anything
/// else that shows a condition say the same thing about it. A reader can then
/// compare two places in the shell without translating between them, and the
/// severity word travels with the colour rather than being implied by it.
/// </summary>
internal static class DiagnosticText
{
    /// <summary>
    /// Describes a diagnostic by its severity, its stable code, and its safe
    /// message. The exception a diagnostic may carry stays out: it belongs to the
    /// log, and a path or payload from it does not belong on screen.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to describe.</param>
    /// <returns>The description, such as <c>Error: VW-PORT-001 …</c>.</returns>
    internal static string Of(NodeDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return $"{SeverityText.Of(diagnostic.Severity)}: {diagnostic.Code} {diagnostic.Message}";
    }
}
