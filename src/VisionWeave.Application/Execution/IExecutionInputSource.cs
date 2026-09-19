using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Supplies the input values of a planned node whose producers are not part of
/// the plan, because they did not change since the previous run. It is the seam
/// the result cache plugs into: without one, a node that depends on an unscheduled
/// producer is reported blocked instead of running on a stale value.
/// </summary>
public interface IExecutionInputSource
{
    /// <summary>
    /// Tries to supply the value of one output port of a producer that is not
    /// part of the plan.
    /// </summary>
    /// <param name="sourceNodeId">The producer node instance identifier.</param>
    /// <param name="sourcePortId">The output port identifier on the producer.</param>
    /// <param name="value">The value when it is available.</param>
    /// <returns><see langword="true"/> when a value is available.</returns>
    bool TryGetValue(Guid sourceNodeId, string sourcePortId, out PortValue? value);
}
