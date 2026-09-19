using Shouldly;
using VisionWeave.Contracts.Execution;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Frames;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// Builds the requests and reads the outputs of a single executor under test, so
/// that a node test states only the parameters, the input, and the outcome it
/// cares about.
/// </summary>
internal static class ExecutorTestRequest
{
    /// <summary>
    /// Creates a parameter set from named values.
    /// </summary>
    /// <param name="values">The parameter name and value pairs.</param>
    /// <returns>The set.</returns>
    internal static NodeParameterSet Parameters(params (string Name, object? Value)[] values)
        => new(values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal));

    /// <summary>
    /// Creates the request of one node execution.
    /// </summary>
    /// <param name="nodeTypeId">The node type being executed.</param>
    /// <param name="parameters">The validated parameters.</param>
    /// <param name="inputs">The bound inputs.</param>
    /// <param name="scope">The resource scope of the execution.</param>
    /// <returns>The request.</returns>
    internal static NodeExecutionRequest For(
        string nodeTypeId,
        NodeParameterSet parameters,
        IReadOnlyDictionary<string, PortValue> inputs,
        IExecutionResourceScope scope)
        => new()
        {
            NodeTypeId = new NodeTypeId(nodeTypeId),
            TypeVersion = 1,
            NodeInstanceId = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            Parameters = parameters,
            Inputs = inputs,
            Resources = scope,
        };

    /// <summary>
    /// Binds an image frame to the image input port.
    /// </summary>
    /// <param name="lease">The frame to bind.</param>
    /// <returns>The inputs.</returns>
    internal static Dictionary<string, PortValue> ImageInput(ImageFrameLease lease)
        => new(StringComparer.Ordinal)
        {
            [OpenCvNodeIds.ImagePortId] = new ImageFrameValue(lease),
        };

    /// <summary>
    /// Reads the frame a node published.
    /// </summary>
    /// <param name="result">The result to read from.</param>
    /// <param name="portId">The output port that carries the frame.</param>
    /// <returns>The produced frame lease.</returns>
    internal static MatFrameLease ImageOutput(NodeExecutionResult result, string portId)
    {
        ArgumentNullException.ThrowIfNull(result);

        result.Outputs.ShouldContainKey(portId);

        return result.Outputs[portId]
            .ShouldBeOfType<ImageFrameValue>()
            .Lease.ShouldBeOfType<MatFrameLease>();
    }
}
