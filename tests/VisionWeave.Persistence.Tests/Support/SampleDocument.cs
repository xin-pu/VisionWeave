using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;
using VisionWeave.Contracts.Workflows;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Persistence.Tests.Support;

/// <summary>
/// The document the persistence tests save and load: two nodes, every parameter
/// value shape the format stores, a connection, a file resource, a remembered port
/// schema, and preserved unknown fields.
/// </summary>
internal static class SampleDocument
{
    internal static NodeTypeId SourceType { get; } = new("visionweave.opencv.image-source");

    internal static NodeTypeId BlurType { get; } = new("visionweave.opencv.gaussian-blur");

    internal static WorkflowDocument Build()
    {
        WorkflowDocument document = WorkflowDocument.Create("Round trip");
        document.RequirePlugin("visionweave.opencv");
        document.RecordAppVersion("0.1.0-test");

        NodeInstance source = document.AddNode(SourceType, 1, new CanvasPosition(12.5, -8));
        NodeInstance blur = document.AddNode(BlurType, 3, new CanvasPosition(480.5, -120));

        document.SetNodeParameter(blur.InstanceId, "kernelSize", 5);
        document.SetNodeParameter(blur.InstanceId, "sigmaX", 1.5);
        document.SetNodeParameter(blur.InstanceId, "normalize", false);
        document.SetNodeParameter(blur.InstanceId, "title", "outer");
        document.SetNodeLabel(blur.InstanceId, "Blur A");
        document.SetNodeEnabled(blur.InstanceId, false);
        document.PreserveExtension("futureField", """{"a":1}""");
        document.PreserveNodeExtension(blur.InstanceId, "futureNodeField", """{"b":[1,2]}""");
        document.AddResource(new FileResourceReference("assets/plate.png", "9f2c1a"));
        document.SetNodePortSchemaSnapshot(
            blur.InstanceId,
            [
                new PortSchemaEntry(
                    "image",
                    PortDirection.Input,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    false,
                    "Image"),
                new PortSchemaEntry("mask", PortDirection.Output),
            ]);
        document.AddConnection(source.InstanceId, "image", blur.InstanceId, "image");

        return document;
    }
}
