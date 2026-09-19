using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Values;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Execution;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>
/// The node a workflow starts from, on a real file: the frame it produces, and
/// every declaration it refuses instead of reading something else (ADR-0012).
/// </summary>
public sealed class ImageSourceExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_reads_the_file_the_workflow_names_beside_itself()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 32, height: 24, value: 200);

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new ImageSourceExecutor(ledger).ExecuteAsync(
            Request(directory, "plate.png", scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);

        MatFrameLease produced = ExecutorTestRequest.ImageOutput(result, OpenCvNodeIds.ImagePortId);
        produced.Width.ShouldBe(32);
        produced.Height.ShouldBe(24);
        produced.PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        produced.IsDisposed.ShouldBeFalse();
        scope.Owned.ShouldBeEmpty("a published frame belongs to the run rather than to the node that produced it.");

        produced.Dispose();
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_without_a_working_directory_refuses_and_creates_no_frame()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 8, height: 8, value: 200);

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new ImageSourceExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ImageSourceTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.PathParameter, "plate.png")),
                new Dictionary<string, PortValue>(StringComparer.Ordinal),
                scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.NodeExecutionFailed);
        result.Diagnostics[0].Message.ShouldContain("Save");
        ledger.Created.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_file_that_is_not_there_fails_with_a_diagnostic()
    {
        using var directory = new TemporaryDirectory();

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new ImageSourceExecutor(ledger).ExecuteAsync(
            Request(directory, "missing.png", scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("missing.png");
        ledger.Created.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_file_that_holds_no_image_fails_with_a_diagnostic()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(directory.File("notes.txt"), "not an image");

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new ImageSourceExecutor(ledger).ExecuteAsync(
            Request(directory, "notes.txt", scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("could not be read as an image");
        ledger.Created.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_a_path_that_leaves_the_workflow_folder_refuses()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 8, height: 8, value: 200);

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new ImageSourceExecutor(ledger).ExecuteAsync(
            Request(directory, "../plate.png", scope),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("leaves the folder");
        ledger.Created.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_without_a_path_parameter_names_no_file()
    {
        using var directory = new TemporaryDirectory();

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();

        NodeExecutionResult result = await new ImageSourceExecutor(ledger).ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.ImageSourceTypeId,
                ExecutorTestRequest.Parameters(),
                new Dictionary<string, PortValue>(StringComparer.Ordinal),
                scope,
                NodeExecutionEnvironment.At(directory.Path)),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("empty");
        ledger.Created.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_creates_no_frame()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("plate.png"), width: 8, height: 8, value: 200);

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new ImageSourceExecutor(ledger).ExecuteAsync(
            Request(directory, "plate.png", scope),
            cancellation.Token));

        ledger.Created.ShouldBe(0);
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        TemporaryDirectory directory,
        string declared,
        IExecutionResourceScope scope)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.ImageSourceTypeId,
            ExecutorTestRequest.Parameters((OpenCvNodeIds.PathParameter, declared)),
            new Dictionary<string, PortValue>(StringComparer.Ordinal),
            scope,
            NodeExecutionEnvironment.At(directory.Path));

    private static void WritePlate(string path, int width, int height, byte value)
    {
        using var plate = new Mat(height, width, MatType.CV_8UC3, Scalar.All(value));

        Cv2.ImWrite(path, plate).ShouldBeTrue();
    }
}
