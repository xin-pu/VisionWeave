using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;

namespace VisionWeave.OpenCv.Nodes;

/// <summary>
/// Supplies the OpenCV node definitions. It is a provider like any other, so the
/// built-in nodes and a future plugin are registered through the same path and
/// the composition root cannot treat one of them specially.
/// <para>
/// The first release contributes two pairs: the frames a file produces and
/// consumes — an image source and a save image, which are the nodes that name a
/// file and therefore the nodes a working directory is resolved for — and the two
/// transforms that prove the pipeline without touching the file system, Gaussian
/// blur and resize.
/// </para>
/// </summary>
public sealed class OpenCvNodeDefinitionProvider : INodeDefinitionProvider
{
    private static readonly IReadOnlyList<NodeDefinition> Definitions =
    [
        CreateImageSource(),
        CreateSaveImage(),
        CreateGaussianBlur(),
        CreateResize(),
    ];

    /// <inheritdoc />
    public string ProviderId => OpenCvNodeIds.ProviderId;

    /// <inheritdoc />
    public IReadOnlyCollection<NodeDefinition> GetDefinitions() => [.. Definitions];

    private static NodeDefinition CreateImageSource()
        => new(
            new NodeTypeId(OpenCvNodeIds.ImageSourceTypeId),
            TypeVersion: 1,
            DisplayName: "Image Source",
            OpenCvNodeIds.InputOutputCategory,
            [
                new PortDefinition(
                    OpenCvNodeIds.ImagePortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [
                // The path is required and has no default, so a definition that no
                // file has been named for yet is a node that does not validate: a
                // source that silently read some file nobody chose would be worse
                // than one that asks.
                new ParameterDefinition(
                    OpenCvNodeIds.PathParameter,
                    ParameterKind.Path,
                    IsRequired: true,
                    DisplayName: "File"),
            ],
            OpenCvNodeIds.ImageSourceExecutorTypeId);

    private static NodeDefinition CreateSaveImage()
        => new(
            new NodeTypeId(OpenCvNodeIds.SaveImageTypeId),
            TypeVersion: 1,
            DisplayName: "Save Image",
            OpenCvNodeIds.InputOutputCategory,
            [
                new PortDefinition(
                    OpenCvNodeIds.ImagePortId,
                    PortDirection.Input,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.PathParameter,
                    ParameterKind.Path,
                    IsRequired: true,
                    DisplayName: "File"),
                new ParameterDefinition(
                    OpenCvNodeIds.OverwriteParameter,
                    ParameterKind.Boolean,
                    IsRequired: false,
                    DisplayName: "Replace an existing file",
                    DefaultValue: false),
            ],
            OpenCvNodeIds.SaveImageExecutorTypeId);

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
