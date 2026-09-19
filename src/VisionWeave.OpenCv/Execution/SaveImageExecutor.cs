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
/// Writes the image a workflow ends with. The file is named relative to the
/// folder the document lives in (ADR-0012), the format is the one the file
/// extension names, and a file that is already there is replaced only when the
/// document says so.
/// <para>
/// The node is a sink: it declares no output port, because what it produces is a
/// file rather than a value another node could read, and a run that reports the
/// write as data would be reporting something no consumer can use. The image is
/// written beside its destination and moved onto it, so a failed or cancelled
/// write never leaves a half-written file where a readable image used to be.
/// </para>
/// </summary>
public sealed class SaveImageExecutor : INodeExecutor
{
    /// <inheritdoc />
    public Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!FrameInput.TryRead(request, out MatFrameLease input, out NodeDiagnostic? failure))
        {
            return Task.FromResult(NodeExecutionResult.Failure(failure!));
        }

        string declared = request.Parameters.Contains(OpenCvNodeIds.PathParameter)
            ? request.Parameters.GetString(OpenCvNodeIds.PathParameter)
            : string.Empty;

        if (!WorkDirectoryPath.TryResolve(
            request.Environment.WorkingDirectory,
            declared,
            out string destination,
            out string? refusal))
        {
            return Task.FromResult(NodeExecutionResult.Failure(FrameInput.Rejected(request, refusal)));
        }

        bool overwrite = request.Parameters.Contains(OpenCvNodeIds.OverwriteParameter)
            && request.Parameters.GetBoolean(OpenCvNodeIds.OverwriteParameter);

        if (File.Exists(destination) && !overwrite)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"The file '{declared}' already exists. Turn on the node's replace parameter to write over it, or name another file.")));
        }

        cancellationToken.ThrowIfCancellationRequested();

        string temporary = TemporaryPathFor(destination);

        try
        {
            if (!Cv2.ImWrite(temporary, input.Mat))
            {
                return Task.FromResult(NodeExecutionResult.Failure(
                    FrameInput.Rejected(request, DescribeUnwritable(declared))));
            }

            File.Move(temporary, destination, overwrite: true);
        }
        catch (OpenCVException exception)
        {
            // A format OpenCV has no writer for arrives this way rather than as a
            // false return, and it is a condition of the document, not a defect.
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(request, DescribeUnwritable(declared), exception)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Task.FromResult(NodeExecutionResult.Failure(
                FrameInput.Rejected(
                    request,
                    $"The file '{declared}' could not be written. Check that the folder exists and that you are allowed to write in it.",
                    exception)));
        }
        finally
        {
            DeleteIfPresent(temporary);
        }

        return Task.FromResult(NodeExecutionResult.Success(
            new Dictionary<string, PortValue>(StringComparer.Ordinal)));
    }

    private static string DescribeUnwritable(string declared)
        => $"The file '{declared}' could not be written as an image. The file extension names the format, so it must be one OpenCV writes, such as .png, .jpg, or .bmp.";

    /// <summary>
    /// Builds the name the image is written under before it replaces its
    /// destination. The extension stays last, because the extension is what names
    /// the format, and the name stays in the destination folder, because a move
    /// across volumes is a copy that a failure can interrupt.
    /// </summary>
    private static string TemporaryPathFor(string destination)
    {
        string directory = Path.GetDirectoryName(destination) ?? string.Empty;
        string name = $"{Path.GetFileNameWithoutExtension(destination)}.{Guid.NewGuid():N}.tmp{Path.GetExtension(destination)}";

        return Path.Combine(directory, name);
    }

    private static void DeleteIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The temporary file is a private artifact of a write that did not
            // finish; failing the node again because it cannot be removed would
            // report the cleanup rather than the write.
        }
    }
}
