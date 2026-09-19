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
/// The node a workflow ends with, on a real file: what it writes, what it refuses
/// to replace, and the temporary file it never leaves behind (ADR-0012).
/// </summary>
public sealed class SaveImageExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_writes_the_image_and_leaves_no_temporary_file_behind()
    {
        using var directory = new TemporaryDirectory();
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = Frame(ledger, width: 16, height: 8, value: 60);

        NodeExecutionResult result = await new SaveImageExecutor().ExecuteAsync(
            Request(directory, "done.png", scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);
        result.Outputs.ShouldBeEmpty("a save node produces a file rather than a value another node could read.");
        input.IsDisposed.ShouldBeFalse("an executor never disposes the frame it received.");

        Directory.GetFiles(directory.Path).ShouldHaveSingleItem().ShouldBe(directory.File("done.png"));

        using Mat written = Cv2.ImRead(directory.File("done.png"), ImreadModes.Color);
        written.Cols.ShouldBe(16);
        written.Rows.ShouldBe(8);
        written.At<Vec3b>(0, 0).Item0.ShouldBe((byte)60);

        input.Dispose();
        ledger.Created.ShouldBe(1);
        ledger.Released.ShouldBe(1);
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_refuses_to_replace_a_file_the_document_did_not_acknowledge()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("done.png"), value: 10);

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = Frame(ledger, width: 16, height: 8, value: 200);

        NodeExecutionResult result = await new SaveImageExecutor().ExecuteAsync(
            Request(directory, "done.png", scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("already exists");

        // The file the run refused to replace is exactly the file that was there.
        using Mat kept = Cv2.ImRead(directory.File("done.png"), ImreadModes.Color);
        kept.At<Vec3b>(0, 0).Item0.ShouldBe((byte)10);

        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_replaces_an_existing_file_when_the_document_says_it_may()
    {
        using var directory = new TemporaryDirectory();
        WritePlate(directory.File("done.png"), value: 10);

        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = Frame(ledger, width: 16, height: 8, value: 200);

        NodeExecutionResult result = await new SaveImageExecutor().ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.SaveImageTypeId,
                ExecutorTestRequest.Parameters(
                    (OpenCvNodeIds.PathParameter, "done.png"),
                    (OpenCvNodeIds.OverwriteParameter, true)),
                ExecutorTestRequest.ImageInput(input),
                scope,
                NodeExecutionEnvironment.At(directory.Path)),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Succeeded);
        Directory.GetFiles(directory.Path).ShouldHaveSingleItem();

        using Mat replaced = Cv2.ImRead(directory.File("done.png"), ImreadModes.Color);
        replaced.At<Vec3b>(0, 0).Item0.ShouldBe((byte)200);

        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_with_an_extension_opencv_cannot_write_fails_and_writes_nothing()
    {
        using var directory = new TemporaryDirectory();
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = Frame(ledger, width: 16, height: 8, value: 60);

        NodeExecutionResult result = await new SaveImageExecutor().ExecuteAsync(
            Request(directory, "done.xyz", scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("extension");
        Directory.GetFiles(directory.Path).ShouldBeEmpty();

        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_without_an_input_image_fails_with_a_diagnostic()
    {
        using var directory = new TemporaryDirectory();

        NodeExecutionResult result = await new SaveImageExecutor().ExecuteAsync(
            ExecutorTestRequest.For(
                OpenCvNodeIds.SaveImageTypeId,
                ExecutorTestRequest.Parameters((OpenCvNodeIds.PathParameter, "done.png")),
                new Dictionary<string, PortValue>(StringComparer.Ordinal),
                new TestResourceScope(),
                NodeExecutionEnvironment.At(directory.Path)),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain(OpenCvNodeIds.ImagePortId);
        Directory.GetFiles(directory.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_with_a_path_that_leaves_the_workflow_folder_refuses_and_writes_nothing()
    {
        using var directory = new TemporaryDirectory();
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = Frame(ledger, width: 16, height: 8, value: 60);

        NodeExecutionResult result = await new SaveImageExecutor().ExecuteAsync(
            Request(directory, "../done.png", scope, input),
            CancellationToken.None);

        result.Status.ShouldBe(NodeExecutionStatus.Failed);
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldContain("leaves the folder");
        Directory.GetFiles(directory.Path).ShouldBeEmpty();

        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_after_cancellation_throws_and_writes_nothing()
    {
        using var directory = new TemporaryDirectory();
        LeaseLedger ledger = new();
        var scope = new TestResourceScope();
        MatFrameLease input = Frame(ledger, width: 16, height: 8, value: 60);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => new SaveImageExecutor().ExecuteAsync(
            Request(directory, "done.png", scope, input),
            cancellation.Token));

        Directory.GetFiles(directory.Path).ShouldBeEmpty();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static NodeExecutionRequest Request(
        TemporaryDirectory directory,
        string declared,
        IExecutionResourceScope scope,
        MatFrameLease input)
        => ExecutorTestRequest.For(
            OpenCvNodeIds.SaveImageTypeId,
            ExecutorTestRequest.Parameters((OpenCvNodeIds.PathParameter, declared)),
            ExecutorTestRequest.ImageInput(input),
            scope,
            NodeExecutionEnvironment.At(directory.Path));

    private static MatFrameLease Frame(ILeaseLedger ledger, int width, int height, byte value)
        => MatFrameLease.Create(new Mat(height, width, MatType.CV_8UC3, Scalar.All(value)), ledger);

    private static void WritePlate(string path, byte value)
    {
        using var plate = new Mat(8, 16, MatType.CV_8UC3, Scalar.All(value));

        Cv2.ImWrite(path, plate).ShouldBeTrue();
    }
}
