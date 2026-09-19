using VisionWeave.Contracts.Nodes;
using VisionWeave.Contracts.Ports;

namespace VisionWeave.OpenCv.Nodes;

/// <summary>
/// Supplies the OpenCV node definitions. It is a provider like any other, so the
/// built-in nodes and a future plugin are registered through the same path and
/// the composition root cannot treat one of them specially.
/// <para>
/// The catalog holds the nodes a file produces and consumes — an image source
/// and a save image, which are the nodes that name a file and therefore the nodes
/// a working directory is resolved for — and the single-image operations a
/// workflow is built from. Gaussian blur and resize prove the pipeline end to end;
/// the Aries migration added the common operations around them: colour conversion,
/// crop, and the pyramids under Transform, a box blur and a bilateral filter beside
/// the two the first build shipped, a threshold that measures each pixel against its
/// own neighbourhood as well as one that compares it with a number, and the four
/// nodes that find an edge: the two gradient measurements, the curvature, and the
/// edge map that follows from a gradient.
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
        CreateCvtColor(),
        CreateCrop(),
        CreateMedianBlur(),
        CreateThreshold(),
        CreateBlur(),
        CreateBilateralFilter(),
        CreateAdaptiveThreshold(),
        CreatePyrDown(),
        CreatePyrUp(),
        CreateSobel(),
        CreateScharr(),
        CreateLaplacian(),
        CreateCanny(),
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

    private static NodeDefinition CreateCvtColor()
        => new(
            new NodeTypeId(OpenCvNodeIds.CvtColorTypeId),
            TypeVersion: 1,
            DisplayName: "Colour Conversion",
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
                    OpenCvNodeIds.ConvertedPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.ConversionParameter,
                    ParameterKind.Option,
                    IsRequired: false,
                    DisplayName: "Conversion",
                    Options: OpenCvNodeIds.ConversionOptions,
                    DefaultValue: OpenCvNodeIds.ConversionGray),
            ],
            OpenCvNodeIds.CvtColorExecutorTypeId);

    private static NodeDefinition CreateCrop()
        => new(
            new NodeTypeId(OpenCvNodeIds.CropTypeId),
            TypeVersion: 1,
            DisplayName: "Crop",
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
                    OpenCvNodeIds.CroppedPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.XParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Left",
                    Minimum: OpenCvParameterBounds.MinOrigin,
                    Maximum: OpenCvParameterBounds.MaxDimension,
                    DefaultValue: 0),
                new ParameterDefinition(
                    OpenCvNodeIds.YParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Top",
                    Minimum: OpenCvParameterBounds.MinOrigin,
                    Maximum: OpenCvParameterBounds.MaxDimension,
                    DefaultValue: 0),
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
            ],
            OpenCvNodeIds.CropExecutorTypeId);

    private static NodeDefinition CreateMedianBlur()
        => new(
            new NodeTypeId(OpenCvNodeIds.MedianBlurTypeId),
            TypeVersion: 1,
            DisplayName: "Median Blur",
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
                    Minimum: OpenCvParameterBounds.MinMedianKernelSize,
                    Maximum: OpenCvParameterBounds.MaxKernelSize,
                    DefaultValue: 3),
            ],
            OpenCvNodeIds.MedianBlurExecutorTypeId);

    private static NodeDefinition CreateThreshold()
        => new(
            new NodeTypeId(OpenCvNodeIds.ThresholdTypeId),
            TypeVersion: 1,
            DisplayName: "Threshold",
            OpenCvNodeIds.ThresholdCategory,
            [
                new PortDefinition(
                    OpenCvNodeIds.ImagePortId,
                    PortDirection.Input,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
                new PortDefinition(
                    OpenCvNodeIds.ThresholdedPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.ThresholdParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Threshold",
                    Minimum: OpenCvParameterBounds.MinLevel,
                    Maximum: OpenCvParameterBounds.MaxLevel,
                    DefaultValue: 122d),
                new ParameterDefinition(
                    OpenCvNodeIds.MaxValueParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Value above the threshold",
                    Minimum: OpenCvParameterBounds.MinLevel,
                    Maximum: OpenCvParameterBounds.MaxLevel,
                    DefaultValue: 255d),
                new ParameterDefinition(
                    OpenCvNodeIds.ThresholdTypeParameter,
                    ParameterKind.Option,
                    IsRequired: false,
                    DisplayName: "Rule",
                    Options: OpenCvNodeIds.ThresholdTypeOptions,
                    DefaultValue: OpenCvNodeIds.ThresholdBinary),
            ],
            OpenCvNodeIds.ThresholdExecutorTypeId);

    private static NodeDefinition CreateBlur()
        => new(
            new NodeTypeId(OpenCvNodeIds.BlurTypeId),
            TypeVersion: 1,
            DisplayName: "Blur",
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
                    DefaultValue: 3),
            ],
            OpenCvNodeIds.BlurExecutorTypeId);

    private static NodeDefinition CreateBilateralFilter()
        => new(
            new NodeTypeId(OpenCvNodeIds.BilateralFilterTypeId),
            TypeVersion: 1,
            DisplayName: "Bilateral Filter",
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
                    OpenCvNodeIds.DiameterParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Neighbourhood diameter",
                    Minimum: OpenCvParameterBounds.MinKernelSize,
                    Maximum: OpenCvParameterBounds.MaxKernelSize,
                    DefaultValue: 5),
                new ParameterDefinition(
                    OpenCvNodeIds.SigmaColorParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Intensity sigma",
                    Minimum: OpenCvParameterBounds.MinSigma,
                    Maximum: OpenCvParameterBounds.MaxBilateralSigma,
                    DefaultValue: 75d),
                new ParameterDefinition(
                    OpenCvNodeIds.SigmaSpaceParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Distance sigma",
                    Minimum: OpenCvParameterBounds.MinSigma,
                    Maximum: OpenCvParameterBounds.MaxBilateralSigma,
                    DefaultValue: 75d),
            ],
            OpenCvNodeIds.BilateralFilterExecutorTypeId);

    private static NodeDefinition CreateAdaptiveThreshold()
        => new(
            new NodeTypeId(OpenCvNodeIds.AdaptiveThresholdTypeId),
            TypeVersion: 1,
            DisplayName: "Adaptive Threshold",
            OpenCvNodeIds.ThresholdCategory,
            [
                new PortDefinition(
                    OpenCvNodeIds.ImagePortId,
                    PortDirection.Input,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
                new PortDefinition(
                    OpenCvNodeIds.ThresholdedPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.MaxValueParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Value above the threshold",
                    Minimum: OpenCvParameterBounds.MinLevel,
                    Maximum: OpenCvParameterBounds.MaxLevel,
                    DefaultValue: 255d),
                new ParameterDefinition(
                    OpenCvNodeIds.AdaptiveMethodParameter,
                    ParameterKind.Option,
                    IsRequired: false,
                    DisplayName: "Average method",
                    Options: OpenCvNodeIds.AdaptiveMethodOptions,
                    DefaultValue: OpenCvNodeIds.AdaptiveMethodMean),
                new ParameterDefinition(
                    OpenCvNodeIds.ThresholdTypeParameter,
                    ParameterKind.Option,
                    IsRequired: false,
                    DisplayName: "Rule",
                    Options: OpenCvNodeIds.AdaptiveThresholdTypeOptions,
                    DefaultValue: OpenCvNodeIds.ThresholdBinary),
                new ParameterDefinition(
                    OpenCvNodeIds.BlockSizeParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Block size",
                    Minimum: OpenCvParameterBounds.MinBlockSize,
                    Maximum: OpenCvParameterBounds.MaxKernelSize,
                    DefaultValue: 11),
                new ParameterDefinition(
                    OpenCvNodeIds.ConstantParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Offset",
                    Minimum: OpenCvParameterBounds.MinConstant,
                    Maximum: OpenCvParameterBounds.MaxConstant,
                    DefaultValue: 5d),
            ],
            OpenCvNodeIds.AdaptiveThresholdExecutorTypeId);

    private static NodeDefinition CreatePyrDown()
        => new(
            new NodeTypeId(OpenCvNodeIds.PyrDownTypeId),
            TypeVersion: 1,
            DisplayName: "Pyramid Down",
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
                    OpenCvNodeIds.ReducedPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [],
            OpenCvNodeIds.PyrDownExecutorTypeId);

    private static NodeDefinition CreatePyrUp()
        => new(
            new NodeTypeId(OpenCvNodeIds.PyrUpTypeId),
            TypeVersion: 1,
            DisplayName: "Pyramid Up",
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
                    OpenCvNodeIds.EnlargedPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Image"),
            ],
            [],
            OpenCvNodeIds.PyrUpExecutorTypeId);

    private static NodeDefinition CreateSobel()
        => new(
            new NodeTypeId(OpenCvNodeIds.SobelTypeId),
            TypeVersion: 1,
            DisplayName: "Sobel",
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
                    OpenCvNodeIds.GradientPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Gradient"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.XOrderParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Horizontal order",
                    Minimum: OpenCvParameterBounds.MinDerivativeOrder,
                    Maximum: OpenCvParameterBounds.MaxDerivativeOrder,
                    DefaultValue: 1),
                new ParameterDefinition(
                    OpenCvNodeIds.YOrderParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Vertical order",
                    Minimum: OpenCvParameterBounds.MinDerivativeOrder,
                    Maximum: OpenCvParameterBounds.MaxDerivativeOrder,
                    DefaultValue: 0),
                new ParameterDefinition(
                    OpenCvNodeIds.KernelSizeParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Kernel size",
                    Minimum: OpenCvParameterBounds.MinKernelSize,
                    Maximum: OpenCvParameterBounds.MaxDerivativeKernelSize,
                    DefaultValue: 3),
                new ParameterDefinition(
                    OpenCvNodeIds.ScaleParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Scale",
                    Minimum: OpenCvParameterBounds.MinScale,
                    Maximum: OpenCvParameterBounds.MaxScale,
                    DefaultValue: 1d),
            ],
            OpenCvNodeIds.SobelExecutorTypeId);

    private static NodeDefinition CreateScharr()
        => new(
            new NodeTypeId(OpenCvNodeIds.ScharrTypeId),
            TypeVersion: 1,
            DisplayName: "Scharr",
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
                    OpenCvNodeIds.GradientPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Gradient"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.XOrderParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Horizontal order",
                    Minimum: OpenCvParameterBounds.MinDerivativeOrder,
                    Maximum: 1,
                    DefaultValue: 1),
                new ParameterDefinition(
                    OpenCvNodeIds.YOrderParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Vertical order",
                    Minimum: OpenCvParameterBounds.MinDerivativeOrder,
                    Maximum: 1,
                    DefaultValue: 0),
                new ParameterDefinition(
                    OpenCvNodeIds.ScaleParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Scale",
                    Minimum: OpenCvParameterBounds.MinScale,
                    Maximum: OpenCvParameterBounds.MaxScale,
                    DefaultValue: 1d),
            ],
            OpenCvNodeIds.ScharrExecutorTypeId);

    private static NodeDefinition CreateLaplacian()
        => new(
            new NodeTypeId(OpenCvNodeIds.LaplacianTypeId),
            TypeVersion: 1,
            DisplayName: "Laplacian",
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
                    OpenCvNodeIds.GradientPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Gradient"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.KernelSizeParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Kernel size",
                    Minimum: OpenCvParameterBounds.MinKernelSize,
                    Maximum: OpenCvParameterBounds.MaxDerivativeKernelSize,
                    DefaultValue: 1),
                new ParameterDefinition(
                    OpenCvNodeIds.ScaleParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Scale",
                    Minimum: OpenCvParameterBounds.MinScale,
                    Maximum: OpenCvParameterBounds.MaxScale,
                    DefaultValue: 1d),
            ],
            OpenCvNodeIds.LaplacianExecutorTypeId);

    private static NodeDefinition CreateCanny()
        => new(
            new NodeTypeId(OpenCvNodeIds.CannyTypeId),
            TypeVersion: 1,
            DisplayName: "Canny",
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
                    OpenCvNodeIds.EdgesPortId,
                    PortDirection.Output,
                    BuiltInPortTypeIds.ImageFrame,
                    PortMultiplicity.Single,
                    IsOptional: false,
                    DisplayName: "Edges"),
            ],
            [
                new ParameterDefinition(
                    OpenCvNodeIds.ThresholdLowParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "Low threshold",
                    Minimum: OpenCvParameterBounds.MinLevel,
                    Maximum: OpenCvParameterBounds.MaxEdgeThreshold,
                    DefaultValue: 50d),
                new ParameterDefinition(
                    OpenCvNodeIds.ThresholdHighParameter,
                    ParameterKind.Number,
                    IsRequired: true,
                    DisplayName: "High threshold",
                    Minimum: OpenCvParameterBounds.MinLevel,
                    Maximum: OpenCvParameterBounds.MaxEdgeThreshold,
                    DefaultValue: 150d),
                new ParameterDefinition(
                    OpenCvNodeIds.ApertureSizeParameter,
                    ParameterKind.Integer,
                    IsRequired: true,
                    DisplayName: "Aperture",
                    Minimum: OpenCvParameterBounds.MinCannyApertureSize,
                    Maximum: OpenCvParameterBounds.MaxCannyApertureSize,
                    DefaultValue: 3),
                new ParameterDefinition(
                    OpenCvNodeIds.L2GradientParameter,
                    ParameterKind.Boolean,
                    IsRequired: false,
                    DisplayName: "More accurate gradient",
                    DefaultValue: false),
            ],
            OpenCvNodeIds.CannyExecutorTypeId);
}
