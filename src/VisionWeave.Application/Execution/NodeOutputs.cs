using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// The outputs one node published during a run, offered to observers such as the
/// preview renderer. Observing an output never transfers ownership: the runtime
/// releases the leases after every observer has finished.
/// </summary>
/// <param name="OperationId">The identifier that correlates the run.</param>
/// <param name="NodeInstanceId">The node that produced the outputs.</param>
/// <param name="NodeTypeId">The node type that produced the outputs.</param>
/// <param name="Values">The produced values keyed by output port identifier.</param>
public sealed record NodeOutputs(
    Guid OperationId,
    Guid NodeInstanceId,
    NodeTypeId NodeTypeId,
    IReadOnlyDictionary<string, PortValue> Values);
