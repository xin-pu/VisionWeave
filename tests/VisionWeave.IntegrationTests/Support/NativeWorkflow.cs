using VisionWeave.Application.Definitions;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.IntegrationTests.Support;

/// <summary>
/// The node catalog a native execution test runs against: a source node that
/// brings a frame into the workflow, plus the built-in OpenCV definitions. The
/// source stays a test definition because the first release has no file-backed
/// input node yet.
/// </summary>
internal static class NativeWorkflow
{
    internal const string SourceExecutorTypeId = "visionweave.tests.image-source-executor";

    internal static NodeTypeId SourceType { get; } = new("visionweave.tests.image-source");

    internal static NodeDefinition SourceDefinition()
        => new(
            SourceType,
            TypeVersion: 1,
            DisplayName: "Image source",
            Category: "Input",
            [
                new PortDefinition(
                    "image",
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            Parameters: [],
            SourceExecutorTypeId);

    internal static NodeDefinitionCatalog Catalog()
        => new([SourceDefinition(), .. new OpenCvNodeDefinitionProvider().GetDefinitions()]);

    /// <summary>
    /// Places an instance of a built-in OpenCV node type in a document.
    /// </summary>
    /// <param name="document">The document to add the node to.</param>
    /// <param name="typeId">The OpenCV node type identifier.</param>
    /// <param name="x">The canvas x coordinate.</param>
    /// <param name="y">The canvas y coordinate.</param>
    /// <returns>The added instance.</returns>
    internal static NodeInstance AddOpenCvNode(WorkflowDocument document, string typeId, double x, double y)
        => document.AddNode(new NodeTypeId(typeId), 1, new CanvasPosition(x, y));
}
