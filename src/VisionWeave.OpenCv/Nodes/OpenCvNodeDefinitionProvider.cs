using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;

namespace VisionWeave.OpenCv.Nodes;

/// <summary>
/// Supplies the OpenCV node definitions. It is a provider like any other, so the
/// built-in nodes and a future plugin are registered through the same path and
/// the composition root cannot treat one of them specially.
/// <para>
/// The first release contributes the two nodes that prove the pipeline without
/// touching the file system: Gaussian blur, which consumes and produces a frame,
/// and resize, which changes the frame geometry. Nodes that read or write files
/// arrive with the path-parameter and export decisions.
/// </para>
/// </summary>
public sealed class OpenCvNodeDefinitionProvider : INodeDefinitionProvider
{
    private static readonly IReadOnlyList<NodeDefinition> Definitions =
    [
        CreateGaussianBlur(),
        CreateResize(),
    ];

    /// <inheritdoc />
    public string ProviderId => OpenCvNodeIds.ProviderId;

    /// <inheritdoc />
    public IReadOnlyCollection<NodeDefinition> GetDefinitions() => [.. Definitions];

    private static NodeDefinition CreateGaussianBlur()
        => new(
            new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId),
            TypeVersion: 1,
            DisplayName: "Gaussian Blur",
            OpenCvNodeIds.FilterCategory,
            [
                new PortDefinition(
                    OpenCvNodeIds.ImagePortId,
                    PortDirection.Input,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
                new PortDefinition(
                    OpenCvNodeIds.BlurredPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.KernelSizeParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Kernel size",
                    Minimum: OpenCvParameterBounds.MinKernelSize,
                    Maximum: OpenCvParameterBounds.MaxKernelSize,
                    DefaultValue: 5),
                new ParameterDefinition(
                    OpenCvNodeIds.SigmaParameter,
                    ParameterKind.Number,
                    IsRequired: false,
                    DisplayName: "Sigma",
                    Minimum: OpenCvParameterBounds.MinSigma,
                    Maximum: OpenCvParameterBounds.MaxSigma,
                    DefaultValue: 0d),
            ],
            OpenCvNodeIds.GaussianBlurExecutorTypeId);

    private static NodeDefinition CreateResize()
        => new(
            new NodeTypeId(OpenCvNodeIds.ResizeTypeId),
            TypeVersion: 1,
            DisplayName: "Resize",
            OpenCvNodeIds.TransformCategory,
            [
                new PortDefinition(
                    OpenCvNodeIds.ImagePortId,
                    PortDirection.Input,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
                new PortDefinition(
                    OpenCvNodeIds.ResizedPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.WidthParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Width",
                    Minimum: OpenCvParameterBounds.MinDimension,
                    Maximum: OpenCvParameterBounds.MaxDimension,
                    DefaultValue: 640),
                new ParameterDefinition(
                    OpenCvNodeIds.HeightParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Height",
                    Minimum: OpenCvParameterBounds.MinDimension,
                    Maximum: OpenCvParameterBounds.MaxDimension,
                    DefaultValue: 480),
                new ParameterDefinition(
                    OpenCvNodeIds.InterpolationParameter,
                    ParameterKind.Option,
                    IsRequired: false,
                    DisplayName: "Interpolation",
                    Options: OpenCvNodeIds.InterpolationOptions,
                    DefaultValue: OpenCvNodeIds.InterpolationArea),
            ],
            OpenCvNodeIds.ResizeExecutorTypeId);
}
