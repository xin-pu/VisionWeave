using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Presentation;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Canvas;

/// <summary>
/// Presents one node instance on the canvas. A node is rebuilt from the committed
/// document, so everything except the two values the gesture layer owns — where
/// the container was dragged to and whether it is selected — is read-only here.
/// The projection decides what a node says; this object is what Nodify binds.
/// </summary>
internal sealed partial class WorkflowNodeViewModel : ObservableObject
{
    /// <summary>
    /// Creates a node presentation.
    /// </summary>
    /// <param name="instanceId">The instance the document holds.</param>
    /// <param name="displayName">The title shown on the node.</param>
    /// <param name="caption">The line under the title that names the type and version.</param>
    /// <param name="position">The committed position of the instance.</param>
    /// <param name="inputs">The input ports, in the order the schema declares them.</param>
    /// <param name="outputs">The output ports, in the order the schema declares them.</param>
    /// <param name="severity">The worst condition the node owns, or <see langword="null"/>.</param>
    /// <param name="condition">The condition that severity belongs to, or an empty string.</param>
    /// <param name="run">What the newest run did to the node, or <see langword="null"/> when no run covers it.</param>
    internal WorkflowNodeViewModel(
        Guid instanceId,
        string displayName,
        string caption,
        CanvasPosition position,
        IReadOnlyList<PortViewModel> inputs,
        IReadOnlyList<PortViewModel> outputs,
        DiagnosticSeverity? severity,
        string condition,
        NodeRunMark? run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(caption);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(condition);

        InstanceId = instanceId;
        DisplayName = displayName;
        Caption = caption;
        Inputs = inputs;
        Outputs = outputs;
        Severity = severity;
        Condition = condition;
        Run = run;
        Location = new Point(position.X, position.Y);

        Summary = Compose(caption, condition, run);
    }

    /// <summary>Gets the instance the document holds, which every edit to this node names.</summary>
    public Guid InstanceId { get; }

    /// <summary>Gets the title shown on the node.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the line under the title that names the type and version.</summary>
    public string Caption { get; }

    /// <summary>Gets the input ports, in the order the schema declares them.</summary>
    public IReadOnlyList<PortViewModel> Inputs { get; }

    /// <summary>Gets the output ports, in the order the schema declares them.</summary>
    public IReadOnlyList<PortViewModel> Outputs { get; }

    /// <summary>Gets the worst condition the node owns, which also picks the node's colour.</summary>
    public DiagnosticSeverity? Severity { get; }

    /// <summary>Gets the condition that severity belongs to, or an empty string.</summary>
    public string Condition { get; }

    /// <summary>
    /// Gets what the newest run did to this node, or <see langword="null"/> when no
    /// run covers it: nothing has run, the run belongs to another document or to a
    /// revision this one has moved past, or the plan never reached the node.
    /// </summary>
    internal NodeRunMark? Run { get; }

    /// <summary>
    /// Gets how the newest run ended for this node, which also picks the colour of
    /// the mark. It is <see langword="null"/> when there is nothing to mark.
    /// </summary>
    public NodeRunState? RunState => Run?.State;

    /// <summary>
    /// Gets what the node says about the run: the outcome and the time the run
    /// measured for it, or nothing when no run covers it.
    /// </summary>
    public string RunDetail
        => Run is { } run ? RunText.Describe(run.State, run.Duration) : string.Empty;

    /// <summary>
    /// Gets the severity as a word, so the node states its condition in text as
    /// well as in colour rather than leaving the colour to carry the meaning.
    /// </summary>
    public string SeverityWord
        => Severity is { } severity ? SeverityText.Of(severity) : string.Empty;

    /// <summary>Gets what the node says about itself when the pointer rests on it.</summary>
    public string Summary { get; }

    /// <summary>
    /// Gets or sets where the container sits, in graph-space coordinates. The
    /// container writes this while a drag is in progress, so the value read here
    /// when the drag ends is the position the user chose.
    /// </summary>
    [ObservableProperty]
    public partial Point Location { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the container is selected. The
    /// container writes this too, and the canvas turns a change into the session's
    /// selection, so the shell reports one selection rather than two.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets where the container sits now, as the document records positions.</summary>
    internal CanvasPosition Position => new(Location.X, Location.Y);

    /// <summary>
    /// Gets where the container sits, rounded to whole graph-space units, which is
    /// the position a completed drag commits.
    /// </summary>
    internal CanvasPosition SnappedPosition => new(Math.Round(Location.X), Math.Round(Location.Y));

    /// <summary>
    /// Writes what the pointer resting on the node reads: what the node is, the
    /// condition the validator reported for it, and what the newest run did to it,
    /// each on its own line and each left out when there is nothing to say.
    /// </summary>
    /// <param name="caption">The line naming the type and version.</param>
    /// <param name="condition">The condition the node owns, or an empty string.</param>
    /// <param name="run">What the newest run did to the node, or <see langword="null"/>.</param>
    /// <returns>The tooltip.</returns>
    private static string Compose(string caption, string condition, NodeRunMark? run)
    {
        string text = caption;

        foreach (string line in new[] { condition, Describe(run), run?.Condition ?? string.Empty })
        {
            if (line.Length > 0)
            {
                text = $"{text}{Environment.NewLine}{line}";
            }
        }

        return text;
    }

    /// <summary>Describes what the run did, or nothing when no run covers the node.</summary>
    private static string Describe(NodeRunMark? run)
        => run is null ? string.Empty : RunText.Describe(run.State, run.Duration);
}
