using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// The preview observer of a native run. It converts every image a node publishes
/// after an await, which is where a preview really happens, and records whether
/// the frame was still alive at that moment and whether the conversion succeeded.
/// </summary>
internal sealed class ConvertingOutputObserver : IExecutionOutputObserver
{
    private readonly object _gate = new();
    private readonly List<PreviewFrame> _previews = [];
    private readonly List<bool> _framesAliveAtConversion = [];
    private readonly FramePreviewConverter _converter;
    private readonly TimeSpan _delay;
    private readonly Action? _onBeforeConvert;
    private int _rejectedFormats;

    internal ConvertingOutputObserver(
        FramePreviewConverter? converter = null,
        TimeSpan? delay = null,
        Action? onBeforeConvert = null)
    {
        _converter = converter ?? FramePreviewConverter.Default;
        _delay = delay ?? TimeSpan.FromMilliseconds(10);
        _onBeforeConvert = onBeforeConvert;
    }

    /// <summary>
    /// Gets the previews that were produced.
    /// </summary>
    internal IReadOnlyList<PreviewFrame> Previews
    {
        get
        {
            lock (_gate)
            {
                return [.. _previews];
            }
        }
    }

    /// <summary>
    /// Gets whether each converted frame was still alive when it was converted.
    /// </summary>
    internal IReadOnlyList<bool> FramesAliveAtConversion
    {
        get
        {
            lock (_gate)
            {
                return [.. _framesAliveAtConversion];
            }
        }
    }

    /// <summary>
    /// Gets the number of frames whose pixel format cannot be previewed.
    /// </summary>
    internal int RejectedFormats => Volatile.Read(ref _rejectedFormats);

    /// <inheritdoc />
    public async Task ObserveAsync(NodeOutputs outputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputs);

        foreach (ImageFrameValue frame in outputs.Values.Values.OfType<ImageFrameValue>())
        {
            if (frame.Lease is not MatFrameLease lease)
            {
                continue;
            }

            // A preview runs off the UI thread and after an await, which is the
            // moment the fence has to keep the frame alive.
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                _framesAliveAtConversion.Add(!lease.IsDisposed);
            }

            _onBeforeConvert?.Invoke();

            try
            {
                PreviewFrame preview = _converter.Convert(lease);

                lock (_gate)
                {
                    _previews.Add(preview);
                }
            }
            catch (NotSupportedException)
            {
                Interlocked.Increment(ref _rejectedFormats);
            }
        }
    }
}
