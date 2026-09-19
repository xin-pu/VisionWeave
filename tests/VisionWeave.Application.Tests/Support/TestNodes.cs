using VisionWeave.Application.Definitions;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;

namespace VisionWeave.Application.Tests.Support;

/// <summary>
/// The node definitions the application tests validate and plan against. They
/// stand in for the built-in OpenCV catalog so that a test never needs the
/// native runtime.
/// </summary>
internal static class TestNodes
{
    public static readonly NodeTypeId SourceType = new("visionweave.test.image-source");

    public static readonly NodeTypeId BlurType = new("visionweave.test.gaussian-blur");

    public static readonly NodeTypeId MaskingType = new("visionweave.test.masking");

    public static readonly NodeTypeId MergeType = new("visionweave.test.merge");

    public static readonly NodeTypeId CountType = new("visionweave.test.count");

    public static readonly NodeTypeId AbsentType = new("visionweave.test.not-provided");

    public static NodeDefinition Source(int version = 1)
        => new(
            SourceType,
            version,
            "Image source",
            "Input",
            [Output("image", BuiltInPortTypeIds.ImageFrame)],
            [],
            "test.image-source");

    public static NodeDefinition Blur(int version = 1)
        => new(
            BlurType,
            version,
            "Gaussian blur",
            "Filter",
            [Input("image", BuiltInPortTypeIds.ImageFrame), Output("blurred", BuiltInPortTypeIds.ImageFrame)],
            [new ParameterDefinition("kernelSize", ParameterKind.Integer, true, "Kernel size", 1, 31, null, 3)],
            "test.gaussian-blur");

    public static NodeDefinition Masking(int version = 1)
        => new(
            MaskingType,
            version,
            "Masking",
            "Filter",
            [
                Input("image", BuiltInPortTypeIds.ImageFrame),
                Input("mask", BuiltInPortTypeIds.ImageFrame, PortMultiplicity.Single, optional: true),
                Output("masked", BuiltInPortTypeIds.ImageFrame),
            ],
            [],
            "test.masking");

    public static NodeDefinition Merge(int version = 1)
        => new(
            MergeType,
            version,
            "Merge frames",
            "Composition",
            [Input("frames", BuiltInPortTypeIds.ImageFrame, PortMultiplicity.Many), Output("image", BuiltInPortTypeIds.ImageFrame)],
            [],
            "test.merge");

    public static NodeDefinition Count(int version = 1)
        => new(
            CountType,
            version,
            "Count regions",
            "Analysis",
            [Input("image", BuiltInPortTypeIds.ImageFrame), Output("count", BuiltInPortTypeIds.Number)],
            [],
            "test.count");

    public static NodeDefinitionCatalog Catalog(params NodeDefinition[] definitions)
        => new(definitions);

    public static NodeDefinitionCatalog DefaultCatalog()
        => new([Source(), Blur(), Masking(), Merge(), Count()]);

    public static PortDefinition Input(
        string id,
        PortTypeId type,
        PortMultiplicity multiplicity = PortMultiplicity.Single,
        bool optional = false)
        => new(id, PortDirection.Input, type, multiplicity, optional, id);

    public static PortDefinition Output(string id, PortTypeId type)
        => new(id, PortDirection.Output, type, PortMultiplicity.Single, false, id);
}
