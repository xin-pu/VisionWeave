using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// The state one run accumulates. It is written from the parallel branches of a
/// level, so every mutation and every snapshot is guarded; the collections it
/// hands out are copies.
/// </summary>
internal sealed class RunState
{
    private readonly object _gate = new();
    private readonly ILeaseLedger _ledger;
    private readonly IExecutionInputSource? _inputs;
    private readonly List<NodeDiagnostic> _diagnostics = [];
    private readonly Dictionary<Guid, NodeRunState> _states = [];
    private readonly Dictionary<Guid, IReadOnlyDictionary<string, PortValue>> _outputs = [];
    private readonly Dictionary<Guid, List<NodeReservation>> _reservations = [];
    private readonly Dictionary<Guid, ExecutionResourceScope> _scopes = [];
    private readonly Dictionary<(Guid NodeId, string PortId), LeasePublication> _publications = [];
    private readonly Dictionary<ImageFrameLease, LeasePublication> _publicationByLease = [];
    private readonly Dictionary<Guid, NodeRunReport> _reports = [];
    private readonly List<Guid> _quarantined = [];
    private readonly long _createdBaseline;
    private readonly long _releasedBaseline;
    private bool _stopped;

    internal RunState(
        ExecutionPlan plan,
        Guid operationId,
        ILeaseLedger ledger,
        ExecutionOptions options,
        IExecutionInputSource? inputs)
    {
        Plan = plan;
        OperationId = operationId;
        _ledger = ledger;
        _inputs = inputs;
        _createdBaseline = ledger.Created;
        _releasedBaseline = ledger.Released;
        Gate = new SemaphoreSlim(Math.Max(1, options.MaxDegreeOfParallelism));
    }

    internal ExecutionPlan Plan { get; }

    internal Guid OperationId { get; }

    /// <summary>
    /// Gets the gate that limits how many nodes execute at the same time.
    /// </summary>
    internal SemaphoreSlim Gate { get; }

    internal bool WasCancelled { get; set; }

    internal IReadOnlyList<Guid> QuarantinedNodeIds => _quarantined;

    /// <summary>
    /// Explains why a node cannot run right now, or returns <see langword="null"/>
    /// when it can.
    /// </summary>
    internal string? FindBlockedReason(Guid nodeId)
    {
        ExecutionPlanNode planned = Plan.GetNode(nodeId);
        Dictionary<string, int> connectionsPerPort = [];

        foreach (NodeInputBinding binding in planned.Inputs)
        {
            connectionsPerPort[binding.TargetPortId] = connectionsPerPort.GetValueOrDefault(binding.TargetPortId) + 1;
            bool optionalInput = IsOptionalInput(planned, binding.TargetPortId);

            if (Plan.TryGetNode(binding.SourceNodeId, out ExecutionPlanNode? producer) && producer is not null)
            {
                if (!_states.TryGetValue(producer.InstanceId, out NodeRunState state))
                {
                    if (optionalInput)
                    {
                        continue;
                    }

                    return $"Node instance '{producer.InstanceId}' has not produced its outputs yet.";
                }

                if (state != NodeRunState.Succeeded)
                {
                    if (optionalInput)
                    {
                        continue;
                    }

                    return $"Node instance '{producer.InstanceId}' ended as {state}, so it produced no value for input port '{binding.TargetPortId}'.";
                }

                continue;
            }

            if (_inputs is null || !_inputs.TryGetValue(binding.SourceNodeId, binding.SourcePortId, out _))
            {
                if (optionalInput)
                {
                    continue;
                }

                return $"No value is available for input port '{binding.TargetPortId}' from node instance '{binding.SourceNodeId}', which is not part of this run.";
            }
        }

        foreach (KeyValuePair<string, int> connection in connectionsPerPort)
        {
            if (connection.Value > 1)
            {
                return $"Input port '{connection.Key}' collects {connection.Value} connections, and this build cannot collect fan-in yet.";
            }
        }

        return null;
    }

    /// <summary>
    /// Collects the values of a node's inputs. The reservations for image frames
    /// were taken when the producing node published its outputs, so this only
    /// reads them.
    /// </summary>
    internal bool TryBindInputs(Guid nodeId, out NodeInputBindingResult binding)
    {
        ExecutionPlanNode planned = Plan.GetNode(nodeId);
        Dictionary<string, PortValue> values = new(StringComparer.Ordinal);

        foreach (NodeInputBinding item in planned.Inputs)
        {
            PortValue? value = null;

            if (_outputs.TryGetValue(item.SourceNodeId, out IReadOnlyDictionary<string, PortValue>? outputs)
                && outputs.TryGetValue(item.SourcePortId, out PortValue? produced))
            {
                value = produced;
            }
            else if (_inputs?.TryGetValue(item.SourceNodeId, item.SourcePortId, out PortValue? cached) == true)
            {
                value = cached;
            }

            if (value is null)
            {
                if (IsOptionalInput(planned, item.TargetPortId))
                {
                    continue;
                }

                binding = NodeInputBindingResult.Blocked(
                    $"Input port '{item.TargetPortId}' has no value.");
                return false;
            }

            values[item.TargetPortId] = value;
        }

        binding = NodeInputBindingResult.Bound(values);
        return true;
    }

    private static bool IsOptionalInput(ExecutionPlanNode planned, string portId)
        => planned.Node.Definition.FindPort(portId)?.IsOptional == true;

    internal void RegisterScope(Guid nodeId, ExecutionResourceScope scope)
    {
        lock (_gate)
        {
            _scopes[nodeId] = scope;
        }
    }

    internal void Succeed(Guid nodeId, TimeSpan duration, IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        Record(nodeId, NodeRunState.Succeeded, duration, diagnostics);
    }

    internal void SetCancelled(Guid nodeId, string code, string message, IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        WasCancelled = true;
        Record(nodeId, NodeRunState.Cancelled, TimeSpan.Zero, [.. diagnostics, new NodeDiagnostic(code, DiagnosticSeverity.Warning, message, nodeId)]);
    }

    internal void Fail(
        Guid nodeId,
        string? code,
        string? message,
        Exception? exception,
        TimeSpan duration = default,
        IReadOnlyList<NodeDiagnostic>? diagnostics = null)
    {
        IReadOnlyList<NodeDiagnostic> reported = diagnostics is { Count: > 0 }
            ? diagnostics
            : [new NodeDiagnostic(
                code ?? DiagnosticCodes.NodeExecutionFailed,
                DiagnosticSeverity.Error,
                message ?? $"Node instance '{nodeId}' failed.",
                nodeId,
                exception)];

        Record(nodeId, NodeRunState.Failed, duration, reported);
    }

    internal void ReportBlocked(Guid nodeId, string reason)
    {
        Record(
            nodeId,
            NodeRunState.Blocked,
            TimeSpan.Zero,
            [new NodeDiagnostic(DiagnosticCodes.NodeExecutionBlocked, DiagnosticSeverity.Error, reason, nodeId)]);

        ReleaseNode(nodeId);
    }

    internal void ReportPreviewFailure(Guid nodeId, Exception exception)
        => AddDiagnostics(
            [
                new NodeDiagnostic(
                    DiagnosticCodes.PreviewConversionFailed,
                    DiagnosticSeverity.Warning,
                    $"The preview of node instance '{nodeId}' could not be produced.",
                    nodeId,
                    exception),
            ]);

    internal IReadOnlyList<NodeDiagnostic> NormalizeDiagnostics(Guid nodeId, Contracts.Execution.NodeExecutionResult result)
        => [.. result.Diagnostics.Select(diagnostic => diagnostic.NodeInstanceId is null ? diagnostic with { NodeInstanceId = nodeId } : diagnostic)];

    /// <summary>
    /// Publishes the outputs of a node that succeeded, together with one
    /// reservation per scheduled consumer and, when a preview observer is
    /// attached, one reservation for the conversion fence.
    /// </summary>
    /// <param name="nodeId">The node that produced the outputs.</param>
    /// <param name="outputs">The produced values.</param>
    /// <param name="withFence">Whether a preview conversion will observe the outputs.</param>
    /// <param name="fences">The fences the caller must release once the conversion completed.</param>
    /// <param name="orphaned">The frame leases to release because the run has already stopped.</param>
    /// <returns><see langword="false"/> when the run had already stopped.</returns>
    internal bool TryPublishOutputs(
        Guid nodeId,
        IReadOnlyDictionary<string, PortValue> outputs,
        bool withFence,
        out List<LeaseFence> fences,
        out IReadOnlyList<IDisposable> orphaned)
    {
        fences = [];

        lock (_gate)
        {
            if (_stopped)
            {
                orphaned = [.. outputs.Values.OfType<ImageFrameValue>().Select(value => value.Lease)];
                return false;
            }

            _outputs[nodeId] = outputs;

            foreach (KeyValuePair<string, PortValue> output in outputs)
            {
                if (output.Value is not ImageFrameValue frame)
                {
                    continue;
                }

                if (!_publicationByLease.TryGetValue(frame.Lease, out LeasePublication? publication))
                {
                    publication = new LeasePublication(frame.Lease, _ledger);
                    _publicationByLease.Add(frame.Lease, publication);
                }

                _publications[(nodeId, output.Key)] = publication;

                foreach (ExecutionPlanNode consumer in Plan.Nodes)
                {
                    foreach (NodeInputBinding binding in consumer.Inputs.Where(
                        item => item.SourceNodeId == nodeId && item.SourcePortId == output.Key))
                    {
                        _reservations.TryAdd(consumer.InstanceId, []);
                        _reservations[consumer.InstanceId].Add(new NodeReservation(publication, publication.Reserve()));
                    }
                }

                if (withFence)
                {
                    fences.Add(new LeaseFence(publication, publication.Reserve()));
                }

                publication.Seal();
            }

            orphaned = [];
            return true;
        }
    }

    internal void ReleaseFences(IEnumerable<LeaseFence> fences)
    {
        foreach (LeaseFence fence in fences)
        {
            fence.Publication.ReleaseReservation(fence.Reservation);
        }
    }

    internal static void ReleaseOrphaned(IEnumerable<IDisposable> orphaned)
    {
        foreach (IDisposable resource in orphaned)
        {
            resource.Dispose();
        }
    }

    /// <summary>
    /// Releases everything one node holds: the reservations taken for its inputs
    /// and the private resources it registered.
    /// </summary>
    internal void ReleaseNode(Guid nodeId)
    {
        List<NodeReservation> reservations;
        ExecutionResourceScope? scope;

        lock (_gate)
        {
            reservations = _reservations.Remove(nodeId, out List<NodeReservation>? taken) ? taken : [];
            scope = _scopes.Remove(nodeId, out ExecutionResourceScope? registered) ? registered : null;
        }

        foreach (NodeReservation reservation in reservations)
        {
            reservation.Publication.ReleaseReservation(reservation.Reservation);
        }

        if (scope is null)
        {
            return;
        }

        scope.Dispose();

        if (scope.DisposalFailures.Count > 0)
        {
            AddDiagnostics(
                [
                    .. scope.DisposalFailures.Select(exception => new NodeDiagnostic(
                        DiagnosticCodes.ResourceDisposalFailed,
                        DiagnosticSeverity.Warning,
                        $"A private resource of node instance '{nodeId}' could not be released.",
                        nodeId,
                        exception)),
                ]);
        }
    }

    /// <summary>
    /// Records a node that ignored cancellation past the grace period. Its
    /// reservations and private resources are deliberately left alone, because
    /// the node may still be reading them; its own completion path releases them.
    /// </summary>
    internal void Quarantine(Guid nodeId)
    {
        if (_quarantined.Contains(nodeId))
        {
            return;
        }

        _quarantined.Add(nodeId);
        AddDiagnostics(
            [
                new NodeDiagnostic(
                    DiagnosticCodes.ExecutorIgnoredCancellation,
                    DiagnosticSeverity.Error,
                    $"Node instance '{nodeId}' did not stop within the cancellation grace period and was quarantined.",
                    nodeId),
            ]);
    }

    /// <summary>
    /// Stops the run and releases what no node can release any more.
    /// </summary>
    internal void Stop()
    {
        Guid[] pending;

        lock (_gate)
        {
            _stopped = true;
            pending = [.. _reservations.Keys.Union(_scopes.Keys).Where(nodeId => !_quarantined.Contains(nodeId))];
        }

        foreach (Guid nodeId in pending)
        {
            ReleaseNode(nodeId);
        }

        LeasePublication[] publications;

        lock (_gate)
        {
            publications = [.. _publications.Values.Distinct()];
        }

        foreach (LeasePublication publication in publications)
        {
            if (publication.RemainingReservations == 0 || _quarantined.Count == 0)
            {
                publication.Release();
            }
        }
    }

    internal IReadOnlyList<NodeRunReport> CollectReports()
    {
        Dictionary<Guid, NodeRunReport> reports;
        HashSet<Guid> quarantined;

        lock (_gate)
        {
            reports = new Dictionary<Guid, NodeRunReport>(_reports);
            quarantined = [.. _quarantined];
        }

        List<NodeRunReport> collected = [];

        foreach (ExecutionPlanNode planned in Plan.Nodes)
        {
            if (reports.TryGetValue(planned.InstanceId, out NodeRunReport? report))
            {
                collected.Add(report);
                continue;
            }

            if (quarantined.Contains(planned.InstanceId))
            {
                collected.Add(new NodeRunReport(
                    planned.InstanceId,
                    NodeRunState.Cancelled,
                    TimeSpan.Zero,
                    [
                        new NodeDiagnostic(
                            DiagnosticCodes.ExecutorIgnoredCancellation,
                            DiagnosticSeverity.Error,
                            $"Node instance '{planned.InstanceId}' was still running when the run stopped.",
                            planned.InstanceId),
                    ]));
                continue;
            }

            collected.Add(new NodeRunReport(planned.InstanceId, NodeRunState.NotRun, TimeSpan.Zero, []));
        }

        return collected;
    }

    internal IReadOnlyList<NodeDiagnostic> CollectDiagnostics(CancellationToken cancellationToken, ILeaseLedger ledger)
    {
        List<NodeDiagnostic> collected;

        lock (_gate)
        {
            collected = [.. _diagnostics];
        }

        if (cancellationToken.IsCancellationRequested)
        {
            WasCancelled = true;
        }

        if (_quarantined.Count > 0)
        {
            // A quarantined node still owns its leases; the leak check would only
            // report what the quarantine already explained.
            return collected;
        }

        long created = ledger.Created - _createdBaseline;
        long released = ledger.Released - _releasedBaseline;

        if (created != released)
        {
            collected.Add(new NodeDiagnostic(
                DiagnosticCodes.LeaseLeaked,
                DiagnosticSeverity.Error,
                $"The run created {created} image frame leases and released {released}."));
        }
        else if (ledger.ReservationsOutstanding != 0)
        {
            collected.Add(new NodeDiagnostic(
                DiagnosticCodes.LeaseLeaked,
                DiagnosticSeverity.Error,
                $"The run left {ledger.ReservationsOutstanding} consumer reservations outstanding."));
        }

        return collected;
    }

    private void Record(Guid nodeId, NodeRunState state, TimeSpan duration, IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        lock (_gate)
        {
            _states[nodeId] = state;
            _reports[nodeId] = new NodeRunReport(nodeId, state, duration, diagnostics);
            _diagnostics.AddRange(diagnostics);
        }
    }

    private void AddDiagnostics(IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        lock (_gate)
        {
            _diagnostics.AddRange(diagnostics);
        }
    }

    private sealed record NodeReservation(LeasePublication Publication, ILeaseReservation Reservation);
}
