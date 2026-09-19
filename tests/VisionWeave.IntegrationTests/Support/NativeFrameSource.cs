using OpenCvSharp;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;

namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// Stands in for a node that brings a frame into the workflow. It creates a real
/// native buffer during the run, wraps it in a lease, and hands the lease over, so
/// a run test observes the same ownership path a file or camera source would use.
/// </summary>
internal sealed class NativeFrameSource
{
    private readonly ILeaseLedger _ledger;
    private readonly List<MatFrameLease> _leases = [];
    private readonly string _portId;
    private readonly int _width;
    private readonly int _height;
    private readonly double _value;

    internal NativeFrameSource(
        ILeaseLedger ledger,
        string portId = "image",
        int width = 8,
        int height = 8,
        double value = 64)
    {
        _ledger = ledger;
        _portId = portId;
        _width = width;
        _height = height;
        _value = value;
    }

    /// <summary>
    /// Gets every frame the source produced, in creation order.
    /// </summary>
    internal IReadOnlyList<MatFrameLease> Leases
    {
        get
        {
            lock (_leases)
            {
                return [.. _leases];
            }
        }
    }

    /// <summary>
    /// Gets the only frame the source produced.
    /// </summary>
    /// <exception cref="InvalidOperationException">The source produced no frame or more than one.</exception>
    internal MatFrameLease Lease
    {
        get
        {
            IReadOnlyList<MatFrameLease> leases = Leases;
            return leases.Count == 1
                ? leases[0]
                : throw new InvalidOperationException($"The source produced {leases.Count} frames, not exactly one.");
        }
    }

    /// <summary>
    /// Gets the executor of the source node.
    /// </summary>
    internal Func<NodeExecutionRequest, CancellationToken, Task<NodeExecutionResult>> Executor
        => (_, _) => Task.FromResult(NodeExecutionResult.Success(Produce()));

    /// <summary>
    /// Creates a native frame and publishes it as the value of the source's output
    /// port.
    /// </summary>
    /// <returns>The produced outputs.</returns>
    internal Dictionary<string, PortValue> Produce()
    {
        var mat = new Mat(_height, _width, MatType.CV_8UC1, Scalar.All(_value));
        MatFrameLease lease = MatFrameLease.Create(mat, _ledger);

        lock (_leases)
        {
            _leases.Add(lease);
        }

        return new Dictionary<string, PortValue>(StringComparer.Ordinal)
        {
            [_portId] = new ImageFrameValue(lease),
        };
    }
}
