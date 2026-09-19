using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// Supplies values for producers that are not part of the plan, which is the seam
/// the result cache will plug into.
/// </summary>
internal sealed class StubInputSource : IExecutionInputSource
{
    private readonly Dictionary<(Guid NodeId, string PortId), PortValue> _values = [];

    /// <summary>
    /// Records the value of one output port of a producer outside the plan.
    /// </summary>
    /// <param name="nodeId">The producer node instance identifier.</param>
    /// <param name="portId">The output port identifier.</param>
    /// <param name="value">The value to supply.</param>
    /// <returns>This source, so registrations can be chained.</returns>
    internal StubInputSource Supply(Guid nodeId, string portId, PortValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portId);
        ArgumentNullException.ThrowIfNull(value);

        _values[(nodeId, portId)] = value;
        return this;
    }

    /// <inheritdoc />
    public bool TryGetValue(Guid sourceNodeId, string sourcePortId, out PortValue? value)
    {
        if (_values.TryGetValue((sourceNodeId, sourcePortId), out PortValue? found))
        {
            value = found;
            return true;
        }

        value = null;
        return false;
    }
}
