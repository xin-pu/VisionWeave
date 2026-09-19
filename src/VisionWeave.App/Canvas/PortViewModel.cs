using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Ports;

namespace VisionWeave.App.Canvas;

/// <summary>
/// Presents one port of one node on the canvas. The anchor is the only part that
/// moves: Nodify's connector control publishes the point a wire attaches to in
/// graph-space coordinates, so that value travels from the control into this
/// object rather than the other way, and a connector rebuilt for a moved node
/// republishes it. Everything else here is read from the projection, so a port
/// shows what the document and its validation say rather than its own opinion.
/// </summary>
internal sealed partial class PortViewModel : ObservableObject
{
    /// <summary>
    /// Creates a port presentation.
    /// </summary>
    /// <param name="nodeInstanceId">The instance the port belongs to.</param>
    /// <param name="portId">The stable, node-local port identifier.</param>
    /// <param name="displayName">The label the port carries.</param>
    /// <param name="direction">Whether the port receives or publishes a value.</param>
    /// <param name="severity">The worst condition the port owns, or <see langword="null"/>.</param>
    /// <param name="condition">The condition that severity belongs to, or an empty string.</param>
    internal PortViewModel(
        Guid nodeInstanceId,
        string portId,
        string displayName,
        PortDirection direction,
        DiagnosticSeverity? severity,
        string condition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(condition);

        NodeInstanceId = nodeInstanceId;
        PortId = portId;
        DisplayName = displayName;
        Direction = direction;
        Severity = severity;
        Condition = condition;

        string description = $"{displayName} · {DirectionWord(direction)}";
        Summary = condition.Length == 0
            ? description
            : $"{description}{Environment.NewLine}{condition}";
    }

    /// <summary>
    /// Gets the instance the port belongs to. A wire is named by its port on both
    /// ends, so an intent carries this alongside <see cref="PortId"/>.
    /// </summary>
    internal Guid NodeInstanceId { get; }

    /// <summary>Gets the stable, node-local port identifier.</summary>
    public string PortId { get; }

    /// <summary>Gets the label the port carries.</summary>
    public string DisplayName { get; }

    /// <summary>Gets whether the port receives or publishes a value.</summary>
    public PortDirection Direction { get; }

    /// <summary>Gets the worst condition the port owns, which also picks its colour.</summary>
    public DiagnosticSeverity? Severity { get; }

    /// <summary>Gets the condition that severity belongs to, or an empty string.</summary>
    public string Condition { get; }

    /// <summary>
    /// Gets what the port says about itself when the pointer rests on it: what it
    /// carries, which way it faces, and any condition it owns.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets or sets where a wire attaches, in graph-space coordinates. The
    /// connector control writes this, so the property is settable and its change
    /// notification is what moves the wires that read it.
    /// </summary>
    [ObservableProperty]
    public partial Point Anchor { get; set; }

    private static string DirectionWord(PortDirection direction)
        => direction == PortDirection.Input ? "Input" : "Output";
}
