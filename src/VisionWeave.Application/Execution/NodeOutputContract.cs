using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// Checks the outputs a successful executor reported against the port contract of
/// its node definition, before the runtime publishes anything. Publication is not
/// a step that can be taken back: it stores the value for the scheduled consumers
/// and takes a reservation per consumer, so a node that names an undeclared port,
/// the wrong port type, or a frame lease it does not own has to be rejected while
/// the only thing to release is what it just reported.
/// <para>
/// Two ownership rules follow from ADR-0005. An executor publishes leases it
/// created, never a lease it received as an input, because the runtime owns the
/// input leases and keeps them alive for the other consumers. A lease has a single
/// owner, so the same lease cannot satisfy two output ports; a node that needs two
/// outputs produces two frames.
/// </para>
/// </summary>
internal static class NodeOutputContract
{
    /// <summary>
    /// Validates the reported outputs of a node.
    /// </summary>
    /// <param name="nodeId">The node instance that reported the outputs.</param>
    /// <param name="definition">The definition that declares the output ports.</param>
    /// <param name="inputs">The values the node received, which it must not report back as its own.</param>
    /// <param name="outputs">The reported outputs, keyed by output port identifier.</param>
    /// <param name="violation">The contract violation when the outputs are rejected.</param>
    /// <returns><see langword="true"/> when the outputs satisfy the contract.</returns>
    internal static bool TryValidate(
        Guid nodeId,
        NodeDefinition definition,
        IReadOnlyDictionary<string, PortValue> inputs,
        IReadOnlyDictionary<string, PortValue> outputs,
        out NodeDiagnostic? violation)
    {
        HashSet<ImageFrameLease> borrowed = [.. inputs.Values.OfType<ImageFrameValue>().Select(value => value.Lease)];
        Dictionary<ImageFrameLease, string> published = [];
        violation = null;

        // Ports are inspected in a stable order so that a node reporting several
        // violations always receives the same diagnostic.
        foreach (KeyValuePair<string, PortValue> output in outputs.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            PortDefinition? port = definition.FindPort(output.Key);

            if (port is null || port.Direction != PortDirection.Output)
            {
                violation = Reject(
                    nodeId,
                    DiagnosticCodes.NodeOutputUndeclared,
                    $"Node instance '{nodeId}' reported an output on port '{output.Key}', which its definition does not declare as an output port.");
                return false;
            }

            if (output.Value is null)
            {
                violation = Reject(
                    nodeId,
                    DiagnosticCodes.NodeOutputTypeMismatch,
                    $"Node instance '{nodeId}' reported no value on output port '{output.Key}', which is declared as port type '{port.TypeId}'.");
                return false;
            }

            if (output.Value.PortTypeId != port.TypeId)
            {
                violation = Reject(
                    nodeId,
                    DiagnosticCodes.NodeOutputTypeMismatch,
                    $"Node instance '{nodeId}' reported port type '{output.Value.PortTypeId}' on output port '{output.Key}', which is declared as port type '{port.TypeId}'.");
                return false;
            }

            if (output.Value is not ImageFrameValue frame)
            {
                continue;
            }

            if (borrowed.Contains(frame.Lease))
            {
                violation = Reject(
                    nodeId,
                    DiagnosticCodes.NodeOutputLeaseNotOwned,
                    $"Node instance '{nodeId}' reported on output port '{output.Key}' an image frame lease it received as an input; an executor publishes a lease it created.");
                return false;
            }

            if (published.TryGetValue(frame.Lease, out string? otherPortId))
            {
                violation = Reject(
                    nodeId,
                    DiagnosticCodes.NodeOutputLeaseAliased,
                    $"Node instance '{nodeId}' reported one image frame lease on output ports '{otherPortId}' and '{output.Key}'; a lease has a single owner.");
                return false;
            }

            published.Add(frame.Lease, output.Key);
        }

        return true;
    }

    private static NodeDiagnostic Reject(Guid nodeId, string code, string message)
        => new(code, DiagnosticSeverity.Error, message, nodeId);
}
