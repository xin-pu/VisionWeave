namespace VisionWeave.OpenCv.Nodes;

/// <summary>
/// The stable identifiers the OpenCV layer contributes: node types, executor
/// registrations, ports, and parameters. They live in one place because they are
/// written into documents and resolved by the composition root, so a rename here
/// is a document migration rather than a refactoring.
/// </summary>
public static class OpenCvNodeIds
{
    /// <summary>The provider identifier shared by the OpenCV definitions.</summary>
    public const string ProviderId = "visionweave.opencv";

    /// <summary>The Gaussian blur node type.</summary>
    public const string GaussianBlurTypeId = "visionweave.opencv.gaussian-blur";

    /// <summary>The resize node type.</summary>
    public const string ResizeTypeId = "visionweave.opencv.resize";

    /// <summary>The node type that reads an image from a file.</summary>
    public const string ImageSourceTypeId = "visionweave.opencv.image-source";

    /// <summary>The node type that writes an image to a file.</summary>
    public const string SaveImageTypeId = "visionweave.opencv.save-image";

    /// <summary>The node type that converts a frame between colour spaces.</summary>
    public const string CvtColorTypeId = "visionweave.opencv.cvt-color";

    /// <summary>The node type that keeps one rectangle of a frame.</summary>
    public const string CropTypeId = "visionweave.opencv.crop";

    /// <summary>The node type that smooths a frame with a median filter.</summary>
    public const string MedianBlurTypeId = "visionweave.opencv.median-blur";

    /// <summary>The node type that separates a frame into foreground and background.</summary>
    public const string ThresholdTypeId = "visionweave.opencv.threshold";

    /// <summary>The node type that smooths a frame with a box filter.</summary>
    public const string BlurTypeId = "visionweave.opencv.blur";

    /// <summary>The node type that smooths a frame while keeping its edges.</summary>
    public const string BilateralFilterTypeId = "visionweave.opencv.bilateral-filter";

    /// <summary>The node type that binarises a frame against its own neighbourhood.</summary>
    public const string AdaptiveThresholdTypeId = "visionweave.opencv.adaptive-threshold";

    /// <summary>The node type that halves a frame.</summary>
    public const string PyrDownTypeId = "visionweave.opencv.pyr-down";

    /// <summary>The node type that doubles a frame.</summary>
    public const string PyrUpTypeId = "visionweave.opencv.pyr-up";

    /// <summary>The executor registration of the Gaussian blur node.</summary>
    public const string GaussianBlurExecutorTypeId = "visionweave.opencv.executor.gaussian-blur";

    /// <summary>The executor registration of the resize node.</summary>
    public const string ResizeExecutorTypeId = "visionweave.opencv.executor.resize";

    /// <summary>The executor registration of the image source node.</summary>
    public const string ImageSourceExecutorTypeId = "visionweave.opencv.executor.image-source";

    /// <summary>The executor registration of the save image node.</summary>
    public const string SaveImageExecutorTypeId = "visionweave.opencv.executor.save-image";

    /// <summary>The executor registration of the colour conversion node.</summary>
    public const string CvtColorExecutorTypeId = "visionweave.opencv.executor.cvt-color";

    /// <summary>The executor registration of the crop node.</summary>
    public const string CropExecutorTypeId = "visionweave.opencv.executor.crop";

    /// <summary>The executor registration of the median blur node.</summary>
    public const string MedianBlurExecutorTypeId = "visionweave.opencv.executor.median-blur";

    /// <summary>The executor registration of the threshold node.</summary>
    public const string ThresholdExecutorTypeId = "visionweave.opencv.executor.threshold";

    /// <summary>The executor registration of the box blur node.</summary>
    public const string BlurExecutorTypeId = "visionweave.opencv.executor.blur";

    /// <summary>The executor registration of the bilateral filter node.</summary>
    public const string BilateralFilterExecutorTypeId = "visionweave.opencv.executor.bilateral-filter";

    /// <summary>The executor registration of the adaptive threshold node.</summary>
    public const string AdaptiveThresholdExecutorTypeId = "visionweave.opencv.executor.adaptive-threshold";

    /// <summary>The executor registration of the pyramid down node.</summary>
    public const string PyrDownExecutorTypeId = "visionweave.opencv.executor.pyr-down";

    /// <summary>The executor registration of the pyramid up node.</summary>
    public const string PyrUpExecutorTypeId = "visionweave.opencv.executor.pyr-up";

    /// <summary>The identifier of a node's image port, whether it receives one or publishes one.</summary>
    public const string ImagePortId = "image";

    /// <summary>The identifier of the smoothed image the Gaussian blur node publishes.</summary>
    public const string BlurredPortId = "blurred";

    /// <summary>The identifier of the image the resize node publishes.</summary>
    public const string ResizedPortId = "resized";

    /// <summary>The identifier of the image the colour conversion node publishes.</summary>
    public const string ConvertedPortId = "converted";

    /// <summary>The identifier of the image the crop node publishes.</summary>
    public const string CroppedPortId = "cropped";

    /// <summary>The identifier of the image the threshold node publishes.</summary>
    public const string ThresholdedPortId = "thresholded";

    /// <summary>The identifier of the smaller image the pyramid down node publishes.</summary>
    public const string ReducedPortId = "reduced";

    /// <summary>The identifier of the larger image the pyramid up node publishes.</summary>
    public const string EnlargedPortId = "enlarged";

    /// <summary>The Gaussian kernel size parameter.</summary>
    public const string KernelSizeParameter = "kernelSize";

    /// <summary>The Gaussian sigma parameter.</summary>
    public const string SigmaParameter = "sigma";

    /// <summary>The target width parameter of the resize node.</summary>
    public const string WidthParameter = "width";

    /// <summary>The target height parameter of the resize node.</summary>
    public const string HeightParameter = "height";

    /// <summary>The interpolation parameter of the resize node.</summary>
    public const string InterpolationParameter = "interpolation";

    /// <summary>The parameter that names the file a node reads or writes.</summary>
    public const string PathParameter = "path";

    /// <summary>The parameter that lets a node replace a file that already exists.</summary>
    public const string OverwriteParameter = "overwrite";

    /// <summary>The colour conversion the colour conversion node performs.</summary>
    public const string ConversionParameter = "conversion";

    /// <summary>The left edge of the rectangle the crop node keeps.</summary>
    public const string XParameter = "x";

    /// <summary>The top edge of the rectangle the crop node keeps.</summary>
    public const string YParameter = "y";

    /// <summary>The threshold the threshold node compares against.</summary>
    public const string ThresholdParameter = "threshold";

    /// <summary>The value the threshold node assigns to the pixels that pass.</summary>
    public const string MaxValueParameter = "maxValue";

    /// <summary>The rule the threshold node applies.</summary>
    public const string ThresholdTypeParameter = "thresholdType";

    /// <summary>The neighbourhood diameter of the bilateral filter node.</summary>
    public const string DiameterParameter = "diameter";

    /// <summary>The intensity sigma of the bilateral filter node.</summary>
    public const string SigmaColorParameter = "sigmaColor";

    /// <summary>The distance sigma of the bilateral filter node.</summary>
    public const string SigmaSpaceParameter = "sigmaSpace";

    /// <summary>The method the adaptive threshold node uses to average a neighbourhood.</summary>
    public const string AdaptiveMethodParameter = "adaptiveMethod";

    /// <summary>The size of the neighbourhood the adaptive threshold node averages.</summary>
    public const string BlockSizeParameter = "blockSize";

    /// <summary>The offset the adaptive threshold node subtracts from the local average.</summary>
    public const string ConstantParameter = "constant";

    /// <summary>The nearest-neighbour interpolation option.</summary>
    public const string InterpolationNearest = "nearest";

    /// <summary>The bilinear interpolation option.</summary>
    public const string InterpolationLinear = "linear";

    /// <summary>The bicubic interpolation option.</summary>
    public const string InterpolationCubic = "cubic";

    /// <summary>The pixel-area interpolation option.</summary>
    public const string InterpolationArea = "area";

    /// <summary>The option that converts a BGR frame to greyscale.</summary>
    public const string ConversionGray = "gray";

    /// <summary>The option that converts a BGR frame to RGB channel order.</summary>
    public const string ConversionRgb = "rgb";

    /// <summary>The option that converts a BGR frame to HSV.</summary>
    public const string ConversionHsv = "hsv";

    /// <summary>The option that converts a BGR frame to CIELAB.</summary>
    public const string ConversionLab = "lab";

    /// <summary>The threshold rule that keeps the pixels above the threshold.</summary>
    public const string ThresholdBinary = "binary";

    /// <summary>The threshold rule that keeps the pixels below the threshold.</summary>
    public const string ThresholdBinaryInverted = "binary-inverted";

    /// <summary>The threshold rule that caps the pixels above the threshold.</summary>
    public const string ThresholdTruncate = "truncate";

    /// <summary>The threshold rule that keeps the pixels above the threshold and zeroes the rest.</summary>
    public const string ThresholdToZero = "to-zero";

    /// <summary>The threshold rule that keeps the pixels below the threshold and zeroes the rest.</summary>
    public const string ThresholdToZeroInverted = "to-zero-inverted";

    /// <summary>The option that compares a pixel with the average of its neighbourhood.</summary>
    public const string AdaptiveMethodMean = "mean";

    /// <summary>The option that compares a pixel with the weighted average of its neighbourhood.</summary>
    public const string AdaptiveMethodGaussian = "gaussian";

    /// <summary>The category of nodes that bring images into a workflow or write them out of it.</summary>
    public const string InputOutputCategory = "Input/Output";

    /// <summary>The category of nodes that change the geometry of a frame.</summary>
    public const string TransformCategory = "Transform";

    /// <summary>The category of nodes that filter a frame.</summary>
    public const string FilterCategory = "Filter";

    /// <summary>The category of nodes that separate a frame into foreground and background.</summary>
    public const string ThresholdCategory = "Threshold";

    /// <summary>The interpolation options the resize node accepts, in the order the editor shows them.</summary>
    public static IReadOnlyList<string> InterpolationOptions { get; } =
    [
        InterpolationNearest,
        InterpolationLinear,
        InterpolationCubic,
        InterpolationArea,
    ];

    /// <summary>The colour conversions the colour conversion node accepts, in the order the editor shows them.</summary>
    public static IReadOnlyList<string> ConversionOptions { get; } =
    [
        ConversionGray,
        ConversionRgb,
        ConversionHsv,
        ConversionLab,
    ];

    /// <summary>The threshold rules the threshold node accepts, in the order the editor shows them.</summary>
    public static IReadOnlyList<string> ThresholdTypeOptions { get; } =
    [
        ThresholdBinary,
        ThresholdBinaryInverted,
        ThresholdTruncate,
        ThresholdToZero,
        ThresholdToZeroInverted,
    ];

    /// <summary>The adaptive methods the adaptive threshold node accepts, in the order the editor shows them.</summary>
    public static IReadOnlyList<string> AdaptiveMethodOptions { get; } =
    [
        AdaptiveMethodMean,
        AdaptiveMethodGaussian,
    ];

    /// <summary>
    /// The rules an adaptive threshold can apply, which are the two OpenCV supports.
    /// They are the option values the threshold node uses, so one rule keeps one
    /// name across both nodes.
    /// </summary>
    public static IReadOnlyList<string> AdaptiveThresholdTypeOptions { get; } =
    [
        ThresholdBinary,
        ThresholdBinaryInverted,
    ];
}
