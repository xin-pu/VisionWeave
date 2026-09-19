using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// The shared setup of the runtime tests: it builds snapshots and plans from the
/// test node catalog and offers the small helpers a run test needs, so that each
/// test states only the workflow and the executors it cares about.
/// </summary>
public abstract class RunnerTestBase
{
    private readonly WorkflowSnapshotFactory _factory = new(TestNodes.DefaultCatalog());
    private readonly ExecutionPlanBuilder _builder = new();

    /// <summary>
    /// Creates a ledger that observes native lease lifetime.
    /// </summary>
    /// <returns>The ledger.</returns>
    protected static LeaseLedger NewLedger() => new();

    /// <summary>
    /// Builds a valid snapshot from a document, failing the test when the document
    /// is not executable.
    /// </summary>
    /// <param name="document">The document to capture.</param>
    /// <returns>The snapshot.</returns>
    protected WorkflowSnapshot Snapshot(WorkflowDocument document)
    {
        SnapshotBuildResult result = _factory.Build(document);
        result.Succeeded.ShouldBeTrue(string.Join(Environment.NewLine, result.Validation.Diagnostics.Select(item => item.Message)));
        return result.Snapshot!;
    }

    /// <summary>
    /// Builds the plan of a document.
    /// </summary>
    /// <param name="document">The document to plan.</param>
    /// <param name="changedNodeIds">The nodes to treat as changed, when the plan is incremental.</param>
    /// <returns>The plan.</returns>
    protected ExecutionPlan Plan(WorkflowDocument document, IEnumerable<Guid>? changedNodeIds = null)
        => _builder.Build(Snapshot(document), changedNodeIds);

    /// <summary>
    /// Creates a runner with the limits a test pins.
    /// </summary>
    /// <param name="executors">The executor resolver.</param>
    /// <param name="ledger">The lease ledger.</param>
    /// <param name="options">The run limits.</param>
    /// <param name="observer">The preview observer, when the test attaches one.</param>
    /// <param name="inputs">The source of values produced outside the plan.</param>
    /// <param name="timeProvider">The clock the run waits and measures on.</param>
    /// <returns>The runner.</returns>
    protected static WorkflowRunner Runner(
        INodeExecutorResolver executors,
        ILeaseLedger ledger,
        ExecutionOptions? options = null,
        IExecutionOutputObserver? observer = null,
        IExecutionInputSource? inputs = null,
        TimeProvider? timeProvider = null)
        => new(executors, ledger, options, observer, inputs, timeProvider);

    /// <summary>
    /// Creates run limits that do not depend on the machine running the test.
    /// </summary>
    /// <param name="parallelism">The greatest number of nodes started at once.</param>
    /// <param name="gracePeriod">How long a node may take to observe cancellation.</param>
    /// <returns>The options.</returns>
    protected static ExecutionOptions Options(int parallelism = 4, TimeSpan? gracePeriod = null)
        => new()
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationGracePeriod = gracePeriod ?? TimeSpan.FromMilliseconds(50),
        };

    /// <summary>
    /// Creates the outputs of a node from named values.
    /// </summary>
    /// <param name="values">The output port identifier and value pairs.</param>
    /// <returns>The outputs.</returns>
    protected static Dictionary<string, PortValue> Outputs(params (string PortId, PortValue Value)[] values)
        => values.ToDictionary(item => item.PortId, item => item.Value, StringComparer.Ordinal);

    /// <summary>
    /// Creates a successful node result that carries outputs.
    /// </summary>
    /// <param name="outputs">The produced outputs.</param>
    /// <returns>The result.</returns>
    protected static NodeExecutionResult Succeeded(IReadOnlyDictionary<string, PortValue> outputs)
        => NodeExecutionResult.Success(outputs, TimeSpan.FromMilliseconds(1));
}
