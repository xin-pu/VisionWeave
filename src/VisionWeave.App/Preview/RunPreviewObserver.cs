using VisionWeave.Application.Definitions;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.App.Preview;

/// <summary>
/// Converts the image a node published into a preview and hands it to the shell. It
/// runs while the runtime still owns the published frame — the task it returns is the
/// fence that keeps the lease alive — so it reads a frame that cannot be released
/// underneath it and hands on a copy that no longer depends on the lease.
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
    /// Converts the image a node published, if it published one, and shows it. A
    /// stopping run is not consulted: the newest image it produced is still worth
    /// showing, and the readout says how the run ended either way.
    /// </summary>
    public async Task ObserveAsync(NodeOutputs outputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputs);

        if (SelectFrame(outputs) is not { } frame)
        {
            return;
        }

        // The lease is still alive here because the runtime waits for this task.
        RunPreview preview = await Task
            .Run(() => Convert(outputs, frame), CancellationToken.None)
            .ConfigureAwait(false);

        await _presenter.PresentAsync(preview).ConfigureAwait(false);
    }

    /// <summary>
    /// Selects the first image output in port order, so which image is shown is a
    /// property of the node's declaration rather than of the order a dictionary
    /// happened to enumerate in. A frame this build cannot convert is passed over: the
    /// node succeeded, and the shell simply has nothing to draw.
    /// </summary>
    private static MatFrameLease? SelectFrame(NodeOutputs outputs)
    {
        foreach (KeyValuePair<string, PortValue> published in outputs.Values.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (published.Value is ImageFrameValue image && image.Lease is MatFrameLease frame)
            {
                return frame;
            }
        }

        return null;
    }

    /// <summary>Converts a frame and names the node that published it.</summary>
    private RunPreview Convert(NodeOutputs outputs, MatFrameLease frame)
    {
        PreviewFrame converted = _converter.Convert(frame);

        // A definition the catalog no longer holds leaves the identifier, which is
        // still worth more than an empty field.
        string title = _catalog.TryResolveLatest(outputs.NodeTypeId, out NodeDefinition? definition) && definition is not null
            ? definition.DisplayName
            : outputs.NodeTypeId.Value;

        return new RunPreview(
            RunPreviewImage.Create(converted),
            outputs.OperationId,
            outputs.NodeInstanceId,
            title,
            converted.Width,
            converted.Height,
            converted.PixelFormat);
    }
}
