using VisionWeave.App.Presentation;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Canvas;

/// <summary>
/// Presents one connection on the canvas. A wire owns no state of its own: its two
/// ends are the port presentations the nodes already expose, so a wire follows the
/// ports it joins when a node moves, and its condition is the validator's answer
/// for the connection itself rather than either endpoint's.
/// </summary>
/// <param name="ConnectionId">The connection the document holds.</param>
/// <param name="Source">The port the wire leaves, which is always an output.</param>
/// <param name="Target">The port the wire arrives at, which is always an input.</param>
/// <param name="Severity">The worst condition the connection owns, or <see langword="null"/>.</param>
/// <param name="Condition">The condition that severity belongs to, or an empty string.</param>
internal sealed record WorkflowConnectionViewModel(
    Guid ConnectionId,
    PortViewModel Source,
    PortViewModel Target,
    DiagnosticSeverity? Severity,
    string Condition)
{
    /// <summary>
    /// Gets the severity as a word, which the wire carries in the middle of the
    /// line so a marked wire says why in text as well as in colour.
    /// </summary>
    public string SeverityWord
        => Severity is { } severity ? SeverityText.Of(severity) : string.Empty;

    /// <summary>
    /// Gets what the wire says about itself when the pointer rests on it, or
    /// <see langword="null"/> when it has nothing to add: an unmarked wire should
    /// open no empty tooltip.
    /// </summary>
    public string? Summary => Condition.Length == 0 ? null : Condition;
}
