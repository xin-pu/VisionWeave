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
}
