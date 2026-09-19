using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// Produces the frame of a node while that node runs, so the lease exists during
/// the run exactly like the native frame a real executor allocates, and a test
/// can inspect the lease before, during, and after the run.
/// </summary>
internal sealed class FrameSource
{
    private readonly object _gate = new();
    private readonly List<FakeFrameLease> _leases = [];
    private readonly ILeaseLedger _ledger;
    private readonly string _portId;

    /// <summary>
    /// Initializes the source.
    /// </summary>
    /// <param name="ledger">The ledger that observes the lease lifetime.</param>
    /// <param name="portId">The output port the produced frame is published on.</param>
    internal FrameSource(ILeaseLedger ledger, string portId = "image")
    {
        _ledger = ledger;
        _portId = portId;
    }

    /// <summary>
    /// Gets the single frame this source produced.
    /// </summary>
    /// <exception cref="InvalidOperationException">No frame, or more than one frame, was produced.</exception>
    internal FakeFrameLease Lease
    {
        get
        {
            lock (_gate)
            {
                return _leases.Count == 1
                    ? _leases[0]
                    : throw new InvalidOperationException($"This source produced {_leases.Count} frames, so 'Lease' is ambiguous.");
            }
        }
    }

    /// <summary>
    /// Gets the frames this source produced, in production order.
    /// </summary>
    internal IReadOnlyList<FakeFrameLease> Leases
    {
        get
        {
            lock (_gate)
            {
                return [.. _leases];
            }
        }
    }

    /// <summary>
    /// Gets an executor that produces one frame and publishes it on the port this
    /// source was created for.
    /// </summary>
    internal Func<NodeExecutionRequest, CancellationToken, Task<NodeExecutionResult>> Executor
        => (_, _) => Task.FromResult(NodeExecutionResult.Success(Produce()));

    /// <summary>
    /// Allocates one frame and returns the outputs that publish it.
    /// </summary>
    /// <returns>The outputs of the producing node.</returns>
    internal Dictionary<string, PortValue> Produce()
        => new(StringComparer.Ordinal) { [_portId] = new ImageFrameValue(ProduceLease()) };

    /// <summary>
    /// Allocates one frame and returns its lease, for a node that owns the frame
    /// itself instead of publishing it.
    /// </summary>
    /// <returns>The lease.</returns>
    internal FakeFrameLease ProduceLease()
    {
        FakeFrameLease lease = FakeFrameLease.Create(_ledger);

        lock (_gate)
        {
            _leases.Add(lease);
        }

        return lease;
    }
}
