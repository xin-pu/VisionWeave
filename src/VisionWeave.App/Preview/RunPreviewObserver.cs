using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.App.Preview;

/// <summary>
/// Converts the images a node published into previews and hands them to the shell. It
/// runs while the runtime still owns the published frames — the task it returns is the
/// fence that keeps the leases alive — so it reads frames that cannot be released
/// underneath it and hands on copies that no longer depend on a lease.
/// </summary>
internal sealed class RunPreviewObserver : IExecutionOutputObserver
{
    private readonly FramePreviewConverter _converter;
    private readonly NodeDefinitionCatalog _catalog;
    private readonly IRunPreviewPresenter _presenter;

    internal RunPreviewObserver(
        FramePreviewConverter converter,
        NodeDefinitionCatalog catalog,
        IRunPreviewPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(presenter);

        _converter = converter;
        _catalog = catalog;
        _presenter = presenter;
    }

    /// <summary>
    /// Converts the images the node published on its image outputs and shows them
    /// together, so a node that splits an image into channels is inspected as a whole
    /// rather than one channel at a time. A stopping run is not consulted: the newest
    /// images it produced are still worth showing, and the readout says how the run
    /// ended either way.
    /// </summary>
    public async Task ObserveAsync(NodeOutputs outputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputs);

        // The leases are still alive here because the runtime waits for this task.
        IReadOnlyList<RunPreview> previews = await Task
            .Run(() => Convert(outputs), CancellationToken.None)
            .ConfigureAwait(false);

        if (previews.Count == 0)
        {
            return;
        }

        await _presenter.PresentAsync(previews).ConfigureAwait(false);
    }

    /// <summary>
    /// Converts every frame the node published, in the order its definition declares the
    /// outputs, so which image stands where is a property of the node's schema rather
    /// than of the order a dictionary happened to enumerate in. A frame this build
    /// cannot convert is passed over: the node succeeded, and the shell draws one image
    /// fewer than the node published rather than refusing the whole node.
    /// </summary>
    private List<RunPreview> Convert(NodeOutputs outputs)
    {
        NodeDefinition? definition = _catalog.TryResolveLatest(outputs.NodeTypeId, out NodeDefinition? resolved)
            ? resolved
            : null;

        // A definition the catalog no longer holds leaves the identifier, which is still
        // worth more than an empty field.
        string title = definition?.DisplayName ?? outputs.NodeTypeId.Value;

        List<RunPreview> previews = [];

        foreach ((string portId, MatFrameLease frame) in PublishedFrames(outputs, definition))
        {
            PreviewFrame converted;
            try
            {
                converted = _converter.Convert(frame);
            }
            catch (NotSupportedException)
            {
                continue;
            }

            previews.Add(new RunPreview(
                RunPreviewImage.Create(converted),
                outputs.OperationId,
                outputs.NodeInstanceId,
                title,
                portId,
                definition?.FindPort(portId)?.DisplayName ?? portId,
                converted.Width,
                converted.Height,
                converted.PixelFormat));
        }

        return previews;
    }

    /// <summary>
    /// The frames a node published, its declared outputs first and any port the
    /// definition does not name after them, so a catalog that has moved on still shows
    /// what the run produced.
    /// </summary>
    private static IEnumerable<(string PortId, MatFrameLease Frame)> PublishedFrames(
        NodeOutputs outputs,
        NodeDefinition? definition)
    {
        IEnumerable<string> declared = definition is null ? [] : definition.Outputs.Select(port => port.Id);
        IEnumerable<string> published = outputs.Values.Keys.OrderBy(key => key, StringComparer.Ordinal);

        foreach (string portId in declared.Concat(published).Distinct(StringComparer.Ordinal))
        {
            if (outputs.Values.TryGetValue(portId, out PortValue? value)
                && value is ImageFrameValue { Lease: MatFrameLease frame })
            {
                yield return (portId, frame);
            }
        }
    }
}
