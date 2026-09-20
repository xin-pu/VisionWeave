using OpenCvSharp;
using Shouldly;
using VisionWeave.Application.Execution;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.IntegrationTests.Support;
using VisionWeave.OpenCv.Execution;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;

namespace VisionWeave.IntegrationTests.OpenCv;

/// <summary>Covers channel operations and basic image transforms migrated from Aries.</summary>
public sealed class ChannelExecutorTests
{
    [Fact]
    public async Task SplitChannels_with_one_bgr_pixel_publishes_three_independent_frames()
    {
        LeaseLedger ledger = new();
        var mat = new Mat(1, 1, MatType.CV_8UC3);
        mat.Set(0, 0, new Vec3b(10, 20, 30));
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new SplitChannelsExecutor(ledger).ExecuteAsync(
            Request(ExecutorTestRequest.ImageInput(input)), CancellationToken.None);

        MatFrameLease blue = Output(result, OpenCvNodeIds.BlueChannelPortId);
        MatFrameLease green = Output(result, OpenCvNodeIds.GreenChannelPortId);
        MatFrameLease red = Output(result, OpenCvNodeIds.RedChannelPortId);
        Pixels(blue).ShouldBe([10]);
        Pixels(green).ShouldBe([20]);
        Pixels(red).ShouldBe([30]);
        blue.Dispose();
        green.Dispose();
        red.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task MergeChannels_with_three_gray_pixels_restores_bgr_order()
    {
        LeaseLedger ledger = new();
        MatFrameLease blue = GrayFrame(ledger, 10);
        MatFrameLease green = GrayFrame(ledger, 20);
        MatFrameLease red = GrayFrame(ledger, 30);
        var inputs = new Dictionary<string, PortValue>(StringComparer.Ordinal)
        {
            [OpenCvNodeIds.BlueChannelPortId] = new ImageFrameValue(blue),
            [OpenCvNodeIds.GreenChannelPortId] = new ImageFrameValue(green),
            [OpenCvNodeIds.RedChannelPortId] = new ImageFrameValue(red),
        };

        NodeExecutionResult result = await new MergeChannelsExecutor(ledger).ExecuteAsync(
            Request(inputs), CancellationToken.None);

        MatFrameLease output = Output(result, OpenCvNodeIds.ResultPortId);
        Pixels(output).ShouldBe([10, 20, 30]);
        output.Dispose();
        blue.Dispose();
        green.Dispose();
        red.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task ExtractChannel_with_red_selected_publishes_the_red_plane()
    {
        LeaseLedger ledger = new();
        var mat = new Mat(1, 1, MatType.CV_8UC3);
        mat.Set(0, 0, new Vec3b(10, 20, 30));
        MatFrameLease input = MatFrameLease.Create(mat, ledger);

        NodeExecutionResult result = await new ExtractChannelExecutor(ledger).ExecuteAsync(
            Request(
                ExecutorTestRequest.ImageInput(input),
                (OpenCvNodeIds.ChannelParameter, OpenCvNodeIds.ChannelRed)),
            CancellationToken.None);

        MatFrameLease output = Output(result, OpenCvNodeIds.ResultPortId);
        Pixels(output).ShouldBe([30]);
        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task CopyMakeBorder_with_constant_fill_surrounds_the_source_pixel()
    {
        LeaseLedger ledger = new();
        MatFrameLease input = GrayFrame(ledger, 10);
        NodeExecutionResult result = await new CopyMakeBorderExecutor(ledger).ExecuteAsync(
            Request(
                ExecutorTestRequest.ImageInput(input),
                (OpenCvNodeIds.TopParameter, 1),
                (OpenCvNodeIds.BottomParameter, 1),
                (OpenCvNodeIds.LeftParameter, 1),
                (OpenCvNodeIds.RightParameter, 1),
                (OpenCvNodeIds.BorderTypeParameter, OpenCvNodeIds.BorderConstant),
                (OpenCvNodeIds.BorderValueParameter, 7d)),
            CancellationToken.None);

        MatFrameLease output = Output(result, OpenCvNodeIds.ResultPortId);
        output.Width.ShouldBe(3);
        output.Height.ShouldBe(3);
        Pixels(output).ShouldBe([7, 7, 7, 7, 10, 7, 7, 7, 7]);
        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Transpose_with_one_column_publishes_one_row()
    {
        LeaseLedger ledger = new();
        var mat = new Mat(2, 1, MatType.CV_8UC1);
        mat.Set(0, 0, (byte)10);
        mat.Set(1, 0, (byte)20);
        MatFrameLease input = MatFrameLease.Create(mat, ledger);
        NodeExecutionResult result = await new TransposeExecutor(ledger).ExecuteAsync(
            Request(ExecutorTestRequest.ImageInput(input)), CancellationToken.None);

        MatFrameLease output = Output(result, OpenCvNodeIds.ResultPortId);
        output.Width.ShouldBe(2);
        output.Height.ShouldBe(1);
        Pixels(output).ShouldBe([10, 20]);
        output.Dispose();
        input.Dispose();
        ledger.Outstanding.ShouldBe(0);
    }

    private static MatFrameLease GrayFrame(LeaseLedger ledger, byte value)
    {
        var mat = new Mat(1, 1, MatType.CV_8UC1);
        mat.Set(0, 0, value);
        return MatFrameLease.Create(mat, ledger);
    }

    private static MatFrameLease Output(NodeExecutionResult result, string portId)
        => ExecutorTestRequest.ImageOutput(result, portId);

    private static byte[] Pixels(MatFrameLease frame)
        => FramePreviewConverter.Default.Convert(frame).Pixels;

    private static NodeExecutionRequest Request(
        IReadOnlyDictionary<string, PortValue> inputs,
        params (string Name, object? Value)[] parameters)
        => new()
        {
            NodeTypeId = new NodeTypeId("visionweave.test.channel"),
            TypeVersion = 1,
            NodeInstanceId = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            Parameters = ExecutorTestRequest.Parameters(parameters),
            Inputs = inputs,
            Resources = new TestResourceScope(),
        };
}
