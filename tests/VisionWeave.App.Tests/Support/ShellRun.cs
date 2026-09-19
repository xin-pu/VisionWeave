using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Builds the run the shell composes, for tests that assemble a shell of their own
/// and never execute a run. It is the wiring the application registers — the real
/// capture, plan builder, executors, and ledger — so a test that drives the shell
/// is not handed a run made of parts the application does not have.
/// </summary>
internal static class ShellRun
{
    /// <summary>Builds the run command over the shell objects a test already holds.</summary>
    /// <param name="session">The session whose document is run.</param>
    /// <param name="catalog">The definitions the document is captured against.</param>
    /// <param name="status">The shell state that reports what the run did.</param>
    /// <param name="boundary">The boundary the run reports its outcome through.</param>
    /// <returns>The command.</returns>
    internal static RunWorkflowCommand CommandFor(
        EditorSession session,
        NodeDefinitionCatalog catalog,
        ShellStatus status,
        AsyncCommandBoundary boundary)
    {
        var ledger = new LeaseLedger();

        return new RunWorkflowCommand(
            boundary,
            session,
            new WorkflowSnapshotFactory(catalog),
            new ExecutionPlanBuilder(),
            new WorkflowRunner(new ExecutorRegistry(ledger), ledger),
            status);
    }
}
