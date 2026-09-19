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

    /// <summary>
    /// Names the element a condition points at, so the inspector can group and
    /// describe its rows without reading the message or knowing what a target is.
    /// A condition that names no target describes the document, which is the scope
    /// it keeps when the element it named is one this build does not model.
    /// </summary>
    /// <param name="target">The element a condition points at, or <see langword="null"/>.</param>
    /// <returns>The phrase the shell shows, such as <c>parameter kernelSize</c>.</returns>
    internal static string TargetOf(DiagnosticTarget? target)
        => target switch
        {
            ParameterTarget parameter => $"parameter {parameter.ParameterName}",
            PortTarget port => $"port {port.PortId}",
            ConnectionTarget connection => $"connection {Short(connection.ConnectionId)}",
            _ => "the document",
        };

    /// <summary>
    /// Shortens an identifier to the part of it a reader can compare by eye. It is
    /// the same prefix the rest of the shell shows, because a full identifier would
    /// only be copied into a message nobody can check.
    /// </summary>
    private static string Short(Guid id) => id.ToString("N")[..8];
}
