using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;

namespace VisionWeave.Contracts.Execution;

/// <summary>
/// Everything one executor needs to run one node. The request contains validated
/// parameters, typed input values, stable identifiers, and the resource scope it
/// must register private resources in. It never contains UI objects.
/// </summary>
public sealed record NodeExecutionRequest
{
    /// <summary>Gets the node type being executed.</summary>
    public required NodeTypeId NodeTypeId { get; init; }

    /// <summary>Gets the definition version resolved for this run.</summary>
    public required int TypeVersion { get; init; }

    /// <summary>Gets the workflow node instance identifier.</summary>
    public required Guid NodeInstanceId { get; init; }

    /// <summary>Gets the identifier that correlates every artifact of one run.</summary>
    public required Guid OperationId { get; init; }

    /// <summary>Gets the validated parameter values.</summary>
    public required NodeParameterSet Parameters { get; init; }

    /// <summary>Gets the input values keyed by input port identifier.</summary>
    public required IReadOnlyDictionary<string, PortValue> Inputs { get; init; }

    /// <summary>Gets the scope that owns the resources the executor creates.</summary>
    public required IExecutionResourceScope Resources { get; init; }
}
