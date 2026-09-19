using OpenCvSharp;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.Preview;
using VisionWeave.App.Sessions;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// The shell's run, assembled the way the application assembles it: one editing session
/// over the real loader, one ledger, the executors this build ships, the capture and the
/// plan builder, and a preview seam the test answers.
/// </summary>
internal sealed class RunHarness : IDisposable
{
    private readonly TemporaryWorkflowDirectory _directory;
    private readonly RecordingPreviewPresenter? _recorded;

    /// <param name="presenter">The preview seam to answer, or a recording one.</param>
    /// <param name="resolvers">Builds the executors the run resolves, which a test that holds a run open replaces.</param>
    internal RunHarness(
        IRunPreviewPresenter? presenter = null,
        Func<ILeaseLedger, INodeExecutorResolver>? resolvers = null)
    {
        _directory = new TemporaryWorkflowDirectory();
        Catalog = NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);
        Validator = new WorkflowValidator(Catalog);
        Ledger = new LeaseLedger();
        Session = new EditorSession(new DocumentLoader(), Validator, TimeProvider.System);
        Status = new ShellStatus(Session);
        Preview = presenter ?? new RecordingPreviewPresenter();
        _recorded = Preview as RecordingPreviewPresenter;

        var boundary = new AsyncCommandBoundary(new RecordingNotificationPresenter(), new RecordingLogger<AsyncCommandBoundary>());
        var observer = new RunPreviewObserver(FramePreviewConverter.Default, Catalog, Preview);
        Executors = resolvers?.Invoke(Ledger) ?? new ExecutorRegistry(Ledger);

        Command = new RunWorkflowCommand(
            boundary,
            Session,
            new WorkflowSnapshotFactory(Catalog),
            new ExecutionPlanBuilder(),
            new WorkflowRunner(Executors, Ledger, ExecutionOptions.Default, observer),
            Status);
    }

    internal NodeDefinitionCatalog Catalog { get; }

    internal WorkflowValidator Validator { get; }

    internal LeaseLedger Ledger { get; }

    internal EditorSession Session { get; }

    internal ShellStatus Status { get; }

    internal IRunPreviewPresenter Preview { get; }

    /// <summary>Gets the executors the run resolves its nodes through, which holds the node a test holds a run at.</summary>
    internal INodeExecutorResolver Executors { get; }

    /// <summary>Gets the previews the run published, or an empty list when the test supplied its own presenter.</summary>
    internal IReadOnlyList<RunPreview> Presented => _recorded?.Presented ?? [];

    internal RunWorkflowCommand Command { get; }

    internal string PathOf(string name) => _directory.PathOf(name);

    /// <summary>Writes an image every channel of which holds one value.</summary>
    internal string WriteImage(string name, int width, int height, byte value)
    {
        string path = PathOf(name);

        using var image = new Mat(height, width, MatType.CV_8UC3, Scalar.All(value));

        Cv2.ImWrite(path, image).ShouldBeTrue();

        return path;
    }

    /// <summary>Adds a node at the version the catalog declares, with the values it is run with.</summary>
    internal Guid Add(string typeId, params (string Name, object? Value)[] parameters)
    {
        var nodeTypeId = new NodeTypeId(typeId);
        int version = Catalog.TryResolveLatest(nodeTypeId, out NodeDefinition? definition) ? definition!.TypeVersion : 1;
        var add = new AddNodeCommand(nodeTypeId, version, new CanvasPosition(0, 0));

        Session.Execute(add).IsAccepted.ShouldBeTrue($"the document must accept a node of type {typeId}.");

        Guid instanceId = add.InstanceId!.Value;

        foreach ((string name, object? value) in parameters)
        {
            Session.Execute(new SetNodeParameterCommand(instanceId, name, value));
        }

        return instanceId;
    }

    /// <summary>Connects two ports the way a drag on the canvas connects them.</summary>
    internal void Connect(Guid sourceNodeId, string sourcePortId, Guid targetNodeId, string targetPortId)
        => Session
            .Execute(new ConnectPortsCommand(Validator, sourceNodeId, sourcePortId, targetNodeId, targetPortId))
            .IsAccepted.ShouldBeTrue("the document must accept a connection between compatible ports.");

    /// <summary>
    /// Writes the document beside the images it uses, which is what gives a run the folder
    /// its relative paths resolve against.
    /// </summary>
    internal string SaveDocument(string name = "plate.vwflow")
    {
        string path = PathOf(name);
        IReadOnlyList<NodeDiagnostic> diagnostics = Session.SaveAs(path);

        diagnostics.ShouldBeEmpty();
        Session.Path.ShouldBe(path);

        return path;
    }

    /// <summary>An image read from the document's own folder, resized, and written back beside it.</summary>
    /// <returns>The three node instance identifiers, in the order they run.</returns>
    internal (Guid Source, Guid Resize, Guid Save) BuildImageWorkflow(
        string inputName = "plate.png",
        string outputName = "done.png",
        int width = 16,
        int height = 8)
    {
        Guid source = Add(
            OpenCvNodeIds.ImageSourceTypeId,
            (OpenCvNodeIds.PathParameter, inputName));

        Guid resize = Add(
            OpenCvNodeIds.ResizeTypeId,
            (OpenCvNodeIds.WidthParameter, width),
            (OpenCvNodeIds.HeightParameter, height));

        Guid save = Add(
            OpenCvNodeIds.SaveImageTypeId,
            (OpenCvNodeIds.PathParameter, outputName));

        Connect(source, OpenCvNodeIds.ImagePortId, resize, OpenCvNodeIds.ImagePortId);
        Connect(resize, OpenCvNodeIds.ResizedPortId, save, OpenCvNodeIds.ImagePortId);

        return (source, resize, save);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
