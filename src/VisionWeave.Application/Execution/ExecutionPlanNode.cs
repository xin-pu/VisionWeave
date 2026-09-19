namespace VisionWeave.Application.Execution;

/// <summary>
/// One node the plan schedules, with every input binding it needs. Bindings are
/// listed for the whole graph, including producers the plan does not schedule,
/// because those values still have to be fetched from cache or from the previous
/// run.
/// </summary>
/// <param name="Node">The resolved snapshot node to execute.</param>
/// <param name="Inputs">The input bindings of the node, in document connection order.</param>
public sealed record ExecutionPlanNode(SnapshotNode Node, IReadOnlyList<NodeInputBinding> Inputs)
{
    /// <summary>
    /// Gets the node instance identifier.
    /// </summary>
    public Guid InstanceId => Node.InstanceId;
}
