using VisionWeave.Contracts.Nodes;

namespace VisionWeave.Application.Execution;

/// <summary>
/// One node of an executable snapshot: the instance identity, the definition
/// version this build resolved for it, and the parameter values the executor
/// reads. A snapshot node never holds canvas state or native resources.
/// </summary>
/// <param name="InstanceId">The document node instance identifier.</param>
/// <param name="Definition">The resolved definition, including its type and version.</param>
/// <param name="Parameters">The parameter values to hand to the executor.</param>
/// <param name="IsEnabled">Whether the instance takes part in a run.</param>
public sealed record SnapshotNode(
    Guid InstanceId,
    NodeDefinition Definition,
    NodeParameterSet Parameters,
    bool IsEnabled);
