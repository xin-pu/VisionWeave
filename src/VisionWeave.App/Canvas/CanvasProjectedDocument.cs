using VisionWeave.Domain.Workflows;

namespace VisionWeave.App.Canvas;

/// <summary>
/// What one document projects onto the canvas: the nodes to draw, in the order
/// they are drawn, and the wires between them. A projection is built from committed
/// document state and replaced whole, so a canvas can never show a graph the
/// document does not hold.
/// </summary>
/// <param name="Nodes">The node presentations, ordered as they are drawn.</param>
/// <param name="Connectors">The wire presentations, in the order the document records them.</param>
internal sealed record CanvasProjectedDocument(
    IReadOnlyList<WorkflowNodeViewModel> Nodes,
    IReadOnlyList<WorkflowConnectionViewModel> Connectors);
