using System.Collections.Frozen;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// The executor registrations the OpenCV layer contributes. The composition root
/// merges them with the registrations of the other providers and resolves a
/// definition by its executor type identifier, so a built-in node and a plugin
/// node are resolved the same way.
/// </summary>
public static class OpenCvExecutors
{
    /// <summary>
    /// Creates the built-in OpenCV executors.
    /// </summary>
    /// <param name="ledger">The ledger the executors report the leases they create to.</param>
    /// <returns>The executors keyed by executor type identifier.</returns>
    public static IReadOnlyDictionary<string, INodeExecutor> CreateDefaults(ILeaseLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        return new Dictionary<string, INodeExecutor>(StringComparer.Ordinal)
        {
            [OpenCvNodeIds.ImageSourceExecutorTypeId] = new ImageSourceExecutor(ledger),
            [OpenCvNodeIds.SaveImageExecutorTypeId] = new SaveImageExecutor(),
            [OpenCvNodeIds.GaussianBlurExecutorTypeId] = new GaussianBlurExecutor(ledger),
            [OpenCvNodeIds.ResizeExecutorTypeId] = new ResizeExecutor(ledger),
            [OpenCvNodeIds.CvtColorExecutorTypeId] = new CvtColorExecutor(ledger),
            [OpenCvNodeIds.NormalizeExecutorTypeId] = new NormalizeExecutor(ledger),
            [OpenCvNodeIds.ConvertScaleAbsExecutorTypeId] = new ConvertScaleAbsExecutor(ledger),
            [OpenCvNodeIds.FlipExecutorTypeId] = new FlipExecutor(ledger),
            [OpenCvNodeIds.SharpenExecutorTypeId] = new SharpenExecutor(ledger),
            [OpenCvNodeIds.InRangeExecutorTypeId] = new InRangeExecutor(ledger),
            [OpenCvNodeIds.BitwiseNotExecutorTypeId] = new BitwiseNotExecutor(ledger),
            [OpenCvNodeIds.EqualizeHistExecutorTypeId] = new EqualizeHistExecutor(ledger),
            [OpenCvNodeIds.RotateExecutorTypeId] = new RotateExecutor(ledger),
            [OpenCvNodeIds.CropExecutorTypeId] = new CropExecutor(ledger),
            [OpenCvNodeIds.MedianBlurExecutorTypeId] = new MedianBlurExecutor(ledger),
            [OpenCvNodeIds.ThresholdExecutorTypeId] = new ThresholdExecutor(ledger),
            [OpenCvNodeIds.BlurExecutorTypeId] = new BlurExecutor(ledger),
            [OpenCvNodeIds.BilateralFilterExecutorTypeId] = new BilateralFilterExecutor(ledger),
            [OpenCvNodeIds.AdaptiveThresholdExecutorTypeId] = new AdaptiveThresholdExecutor(ledger),
            [OpenCvNodeIds.PyrDownExecutorTypeId] = new PyrDownExecutor(ledger),
            [OpenCvNodeIds.PyrUpExecutorTypeId] = new PyrUpExecutor(ledger),
            [OpenCvNodeIds.SobelExecutorTypeId] = new SobelExecutor(ledger),
            [OpenCvNodeIds.ScharrExecutorTypeId] = new ScharrExecutor(ledger),
            [OpenCvNodeIds.LaplacianExecutorTypeId] = new LaplacianExecutor(ledger),
            [OpenCvNodeIds.CannyExecutorTypeId] = new CannyExecutor(ledger),
            [OpenCvNodeIds.ErodeExecutorTypeId] = new ErodeExecutor(ledger),
            [OpenCvNodeIds.DilateExecutorTypeId] = new DilateExecutor(ledger),
            [OpenCvNodeIds.MorphologyExExecutorTypeId] = new MorphologyExExecutor(ledger),
            [OpenCvNodeIds.DrawRectangleExecutorTypeId] = new DrawRectangleExecutor(ledger),
            [OpenCvNodeIds.DrawLineExecutorTypeId] = new DrawLineExecutor(ledger),
            [OpenCvNodeIds.DrawCircleExecutorTypeId] = new DrawCircleExecutor(ledger),
            [OpenCvNodeIds.FindContoursExecutorTypeId] = new FindContoursExecutor(),
            [OpenCvNodeIds.DrawContoursExecutorTypeId] = new DrawContoursExecutor(ledger),
        }.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
