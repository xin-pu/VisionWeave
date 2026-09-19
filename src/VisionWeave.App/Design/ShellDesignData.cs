using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.Notifications;
using VisionWeave.App.Preview;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.App.Design;

/// <summary>
/// The sample the XAML designer shows instead of an empty window. It builds the
/// objects the shell builds at run time — the real catalog, a real session, and
/// edits that go through the command history — so the designer cannot present a
/// shell, a document, or a catalogue the application would never produce.
/// </summary>
public static class ShellDesignData
{
    private static readonly Lazy<object> Sample = new(Create);

    /// <summary>
    /// Gets the sample content for the shell's data context. It is returned as an
    /// object so the view model stays internal to the application while the
    /// designer, which loads the assembly from the outside, can still bind to it.
    /// </summary>
    public static object ViewModel => Sample.Value;

    private static object Create()
    {
        NodeDefinitionCatalog catalog = NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);
        var validator = new WorkflowValidator(catalog);
        EditorSession session = new(new DocumentLoader(), validator, TimeProvider.System);
        ShellStatus status = new(session);
        PreviewViewModel preview = new(session);
        var boundary = new AsyncCommandBoundary(new SilentPresenter(), NullLogger<AsyncCommandBoundary>.Instance);

        Guid blur = AddNode(session, catalog, OpenCvNodeIds.GaussianBlurTypeId, new CanvasPosition(0, 0));
        Guid resize = AddNode(session, catalog, OpenCvNodeIds.ResizeTypeId, new CanvasPosition(280, 0));

        session.Execute(new ConnectPortsCommand(
            validator,
            blur,
            OpenCvNodeIds.BlurredPortId,
            resize,
            OpenCvNodeIds.ImagePortId));

        // The sample carries one condition, so the designer shows the inspector's
        // list the way a document with something to say draws it: the value is
        // outside the range its definition declares.
        session.Execute(new SetNodeParameterCommand(blur, OpenCvNodeIds.KernelSizeParameter, 5));
        session.Execute(new SetNodeParameterCommand(blur, OpenCvNodeIds.KernelSizeParameter, 4096));
        session.Select([blur]);

        return new MainWindowViewModel(
            session,
            catalog,
            validator,
            new OpenDocumentCommand(boundary, session, status),
            new SaveDocumentCommand(boundary, session, status, new SilentFileChooser()),
            RunWorkflow(session, catalog, status, boundary, preview),
            preview,
            new SilentFileChooser(),
            new ShellPromptViewModel(),
            status);
    }

    /// <summary>
    /// Builds the sample's Run action out of the objects the application composes:
    /// the same ledger, executors, capture, plan builder, runtime, and preview. The
    /// sample document has never been saved, so running it is refused the way the
    /// application refuses it — which is a state the designer should show rather
    /// than one it should invent a result for.
    /// </summary>
    /// <param name="session">The session whose document is run.</param>
    /// <param name="catalog">The definitions the sample document is captured against.</param>
    /// <param name="status">The shell state that reports what the run did.</param>
    /// <param name="boundary">The boundary that runs the operation and reports its outcome.</param>
    /// <param name="preview">The preview the sample shell draws.</param>
    /// <returns>The sample's run command.</returns>
    private static RunWorkflowCommand RunWorkflow(
        EditorSession session,
        NodeDefinitionCatalog catalog,
        ShellStatus status,
        AsyncCommandBoundary boundary,
        PreviewViewModel preview)
    {
        var ledger = new LeaseLedger();

        return new RunWorkflowCommand(
            boundary,
            session,
            new WorkflowSnapshotFactory(catalog),
            new ExecutionPlanBuilder(),
            new WorkflowRunner(
                new ExecutorRegistry(ledger),
                ledger,
                ExecutionOptions.Default,
                new RunPreviewObserver(
                    FramePreviewConverter.Default,
                    catalog,
                    new ShellRunPreviewPresenter(preview, Dispatcher.CurrentDispatcher))),
            status);
    }

    /// <summary>
    /// Adds a node at the version the catalog declares, so the sample document
    /// validates instead of presenting a node the catalog would call unknown.
    /// </summary>
    private static Guid AddNode(
        EditorSession session,
        NodeDefinitionCatalog catalog,
        string typeId,
        CanvasPosition position)
    {
        var id = new NodeTypeId(typeId);
        int version = catalog.TryResolveLatest(id, out NodeDefinition? definition) ? definition!.TypeVersion : 1;

        var command = new AddNodeCommand(id, version, position);
        session.Execute(command);

        return command.InstanceId!.Value;
    }

    /// <summary>
    /// A presenter for the sample, which is created before the shell has a window
    /// and therefore has nothing to show a message in.
    /// </summary>
    private sealed class SilentPresenter : IUserNotificationPresenter
    {
        /// <inheritdoc />
        public void Present(NodeDiagnostic diagnostic)
        {
        }
    }

    /// <summary>A file chooser for the sample, which never opens a dialog.</summary>
    private sealed class SilentFileChooser : IWorkflowFileChooser
    {
        /// <inheritdoc />
        public string? ChooseDocumentToOpen(string? currentPath) => null;

        /// <inheritdoc />
        public string? ChooseDocumentToSave(string? currentPath, string suggestedName) => null;
    }
}
