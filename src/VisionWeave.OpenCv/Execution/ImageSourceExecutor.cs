using System.Diagnostics;
using OpenCvSharp;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Files;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.OpenCv.Execution;

/// <summary>
/// Reads the image a workflow starts from. The file is named relative to the
/// folder the document lives in (ADR-0012), so the same workflow reads the same
/// plate after the folder is copied somewhere else, and a path that leaves that
/// folder is refused rather than followed.
/// <para>
/// The image is read as a color frame, which is what OpenCV's own writer and the
/// preview converter both expect, and the frame is published as a lease this
/// executor created and no longer owns.
/// </para>
/// </summary>
public sealed class ImageSourceExecutor : INodeExecutor
{
    private readonly ILeaseLedger _ledger;

    /// <summary>
    /// Initializes the executor.
    /// </summary>
    /// <param name="ledger">The ledger that observes the lease this executor creates.</param>
    public ImageSourceExecutor(ILeaseLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        _ledger = ledger;
    }

    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Stopwatch stopwatch = Stopwatch.StartNew();

        string declared = ParameterText(request, OpenCvNodeIds.PathParameter);

        if (!WorkDirectoryPath.TryResolve(
            request.Environment.WorkingDirectory,
            declared,
            out string path,
            out string? refusal))
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(request, refusal), stopwatch.Elapsed));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Reading is the part of this node that blocks, and it happens on the thread
        // the runtime scheduled the node on rather than on the one that draws a
        // window.
        Mat read = Cv2.ImRead(path, ImreadModes.Color);
        bool transferred = false;

        try
        {
            if (read.Empty())
            {
                return Task.FromResult(NodeExecutionResult.Failure(
                    FrameInput.Rejected(
                        request,
                        $"The file '{declared}' could not be read as an image. Check that it exists beside the workflow and that its format is one OpenCV reads."),
                    stopwatch.Elapsed));
            }

            // The lease owns the mat from here on, exactly as it owns one this
            // executor allocated: the runtime releases it once its consumers are done.
            MatFrameLease produced = MatFrameLease.Create(read, _ledger);
            transferred = true;
            stopwatch.Stop();

            return Task.FromResult(NodeExecutionResult.Success(
                new Dictionary<string, PortValue>(StringComparer.Ordinal)
                {
                    [OpenCvNodeIds.ImagePortId] = new ImageFrameValue(produced),
                },
                stopwatch.Elapsed));
        }
        finally
        {
            if (!transferred)
            {
                read.Dispose();
            }
        }
    }

    private static string ParameterText(NodeExecutionRequest request, string name)
        => request.Parameters.Contains(name) ? request.Parameters.GetString(name) : string.Empty;
}
