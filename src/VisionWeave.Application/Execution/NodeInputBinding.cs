namespace VisionWeave.Application.Execution;

/// <summary>
/// Binds one input port of a scheduled node to the output port that feeds it.
/// A node with several bindings for one port receives its values in binding
/// order, which is the document's connection order.
/// </summary>
/// <param name="TargetPortId">The input port identifier on the consuming node.</param>
/// <param name="SourceNodeId">The node instance that produces the value.</param>
/// <param name="SourcePortId">The output port identifier on the producing node.</param>
public sealed record NodeInputBinding(string TargetPortId, Guid SourceNodeId, string SourcePortId);
