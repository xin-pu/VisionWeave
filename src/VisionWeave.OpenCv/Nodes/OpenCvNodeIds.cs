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

    /// <summary>The node type that reports the gradient of a frame along one or both axes.</summary>
    public const string SobelTypeId = "visionweave.opencv.sobel";

    /// <summary>The node type that reports the gradient of a frame with a three by three kernel.</summary>
    public const string ScharrTypeId = "visionweave.opencv.scharr";

    /// <summary>The node type that reports the curvature of a frame.</summary>
    public const string LaplacianTypeId = "visionweave.opencv.laplacian";

    /// <summary>The node type that reports the edges of a frame.</summary>
    public const string CannyTypeId = "visionweave.opencv.canny";

    /// <summary>The node type that thins bright regions by a structuring element.</summary>
    public const string ErodeTypeId = "visionweave.opencv.erode";

    /// <summary>The node type that thickens bright regions by a structuring element.</summary>
    public const string DilateTypeId = "visionweave.opencv.dilate";

    /// <summary>The node type that opens, closes, or outlines bright regions.</summary>
    public const string MorphologyExTypeId = "visionweave.opencv.morphology-ex";

    /// <summary>The node type that marks a rectangle on a frame.</summary>
    public const string DrawRectangleTypeId = "visionweave.opencv.draw-rectangle";

    /// <summary>The node type that marks a line on a frame.</summary>
    public const string DrawLineTypeId = "visionweave.opencv.draw-line";

    /// <summary>The node type that marks a circle on a frame.</summary>
    public const string DrawCircleTypeId = "visionweave.opencv.draw-circle";

    /// <summary>The node type that finds the contours of a mask.</summary>
    public const string FindContoursTypeId = "visionweave.opencv.find-contours";

    /// <summary>The node type that draws contours onto a frame.</summary>
    public const string DrawContoursTypeId = "visionweave.opencv.draw-contours";

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

    /// <summary>The executor registration of the Sobel node.</summary>
    public const string SobelExecutorTypeId = "visionweave.opencv.executor.sobel";

    /// <summary>The executor registration of the Scharr node.</summary>
    public const string ScharrExecutorTypeId = "visionweave.opencv.executor.scharr";

    /// <summary>The executor registration of the Laplacian node.</summary>
    public const string LaplacianExecutorTypeId = "visionweave.opencv.executor.laplacian";

    /// <summary>The executor registration of the Canny node.</summary>
    public const string CannyExecutorTypeId = "visionweave.opencv.executor.canny";

    /// <summary>The executor registration of the erode node.</summary>
    public const string ErodeExecutorTypeId = "visionweave.opencv.executor.erode";

    /// <summary>The executor registration of the dilate node.</summary>
    public const string DilateExecutorTypeId = "visionweave.opencv.executor.dilate";

    /// <summary>The executor registration of the morphology ex node.</summary>
    public const string MorphologyExExecutorTypeId = "visionweave.opencv.executor.morphology-ex";

    /// <summary>The executor registration of the draw rectangle node.</summary>
    public const string DrawRectangleExecutorTypeId = "visionweave.opencv.executor.draw-rectangle";

    /// <summary>The executor registration of the draw line node.</summary>
    public const string DrawLineExecutorTypeId = "visionweave.opencv.executor.draw-line";

    /// <summary>The executor registration of the draw circle node.</summary>
    public const string DrawCircleExecutorTypeId = "visionweave.opencv.executor.draw-circle";

    /// <summary>The executor registration of the find contours node.</summary>
    public const string FindContoursExecutorTypeId = "visionweave.opencv.executor.find-contours";

    /// <summary>The executor registration of the draw contours node.</summary>
    public const string DrawContoursExecutorTypeId = "visionweave.opencv.executor.draw-contours";

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

    /// <summary>The identifier of the gradient the derivative nodes publish.</summary>
    public const string GradientPortId = "gradient";

    /// <summary>The identifier of the edge map the Canny node publishes.</summary>
    public const string EdgesPortId = "edges";

    /// <summary>The identifier of the image the erode node publishes.</summary>
    public const string ErodedPortId = "eroded";

    /// <summary>The identifier of the image the dilate node publishes.</summary>
    public const string DilatedPortId = "dilated";

    /// <summary>The identifier of the image the morphology ex node publishes.</summary>
    public const string MorphedPortId = "morphed";

    /// <summary>The identifier of the image a draw node publishes.</summary>
    public const string DrawnPortId = "drawn";

    /// <summary>
    /// The identifier of a contour set: the output of the node that finds one, and the
    /// input of the node that draws one, because the value that leaves the first enters
    /// the second under the same name.
    /// </summary>
    public const string ContoursPortId = "contours";

    /// <summary>The Gaussian kernel size parameter.</summary>
    public const string KernelSizeParameter = "kernelSize";

    /// <summary>The Gaussian sigma parameter.</summary>
    public const string SigmaParameter = "sigma";

    /// <summary>The width a node produces or marks.</summary>
    public const string WidthParameter = "width";

    /// <summary>The height a node produces or marks.</summary>
    public const string HeightParameter = "height";

    /// <summary>The interpolation parameter of the resize node.</summary>
    public const string InterpolationParameter = "interpolation";

    /// <summary>The parameter that names the file a node reads or writes.</summary>
    public const string PathParameter = "path";

    /// <summary>The parameter that lets a node replace a file that already exists.</summary>
    public const string OverwriteParameter = "overwrite";

    /// <summary>The colour conversion the colour conversion node performs.</summary>
    public const string ConversionParameter = "conversion";

    /// <summary>The left edge of a rectangle the node keeps or marks.</summary>
    public const string XParameter = "x";

    /// <summary>The top edge of a rectangle the node keeps or marks.</summary>
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

    /// <summary>The derivative order the derivative nodes apply across the columns.</summary>
    public const string XOrderParameter = "xOrder";

    /// <summary>The derivative order the derivative nodes apply down the rows.</summary>
    public const string YOrderParameter = "yOrder";

    /// <summary>The factor a derivative node applies to the result before it is reported.</summary>
    public const string ScaleParameter = "scale";

    /// <summary>The gradient below which the Canny node discards a pixel.</summary>
    public const string ThresholdLowParameter = "thresholdLow";

    /// <summary>The gradient above which the Canny node keeps a pixel.</summary>
    public const string ThresholdHighParameter = "thresholdHigh";

    /// <summary>The size of the kernel the Canny node measures the gradient with.</summary>
    public const string ApertureSizeParameter = "apertureSize";

    /// <summary>The switch that lets the Canny node measure the gradient more accurately.</summary>
    public const string L2GradientParameter = "l2gradient";

    /// <summary>The shape of the structuring element a morphology node works with.</summary>
    public const string KernelShapeParameter = "kernelShape";

    /// <summary>The number of times a morphology node applies its structuring element.</summary>
    public const string IterationsParameter = "iterations";

    /// <summary>The morphological operation the morphology ex node applies.</summary>
    public const string OperationParameter = "operation";

    /// <summary>The left end of the line the line node draws.</summary>
    public const string StartXParameter = "startX";

    /// <summary>The top end of the line the line node draws.</summary>
    public const string StartYParameter = "startY";

    /// <summary>The right end of the line the line node draws.</summary>
    public const string EndXParameter = "endX";

    /// <summary>The bottom end of the line the line node draws.</summary>
    public const string EndYParameter = "endY";

    /// <summary>The radius of the circle the circle node draws.</summary>
    public const string RadiusParameter = "radius";

    /// <summary>The blue component of the colour a draw node marks with.</summary>
    public const string BlueParameter = "blue";

    /// <summary>The green component of the colour a draw node marks with.</summary>
    public const string GreenParameter = "green";

    /// <summary>The red component of the colour a draw node marks with.</summary>
    public const string RedParameter = "red";

    /// <summary>The width of the outline a draw node marks with.</summary>
    public const string ThicknessParameter = "thickness";

    /// <summary>The switch that makes a draw node mark the inside of its shape as well as its outline.</summary>
    public const string FilledParameter = "filled";

    /// <summary>The switch that decides which contours the find contours node reports.</summary>
    public const string RetrievalParameter = "retrieval";

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

    /// <summary>The option that builds a structuring element that is a filled rectangle.</summary>
    public const string KernelShapeRect = "rect";

    /// <summary>The option that builds a structuring element that is a filled ellipse.</summary>
    public const string KernelShapeEllipse = "ellipse";

    /// <summary>The option that builds a structuring element that is a plus sign.</summary>
    public const string KernelShapeCross = "cross";

    /// <summary>The operation that erodes and then dilates, which removes what is smaller than the kernel.</summary>
    public const string MorphologyOpen = "open";

    /// <summary>The operation that dilates and then erodes, which fills what is smaller than the kernel.</summary>
    public const string MorphologyClose = "close";

    /// <summary>The operation that reports the difference between the dilated and the eroded frame.</summary>
    public const string MorphologyGradient = "gradient";

    /// <summary>The operation that reports what the opening removed.</summary>
    public const string MorphologyTopHat = "top-hat";

    /// <summary>The operation that reports what the closing filled.</summary>
    public const string MorphologyBlackHat = "black-hat";

    /// <summary>
    /// The retrieval option that reports the outermost contours only, which is what
    /// "how many objects are in this mask, and where" asks for: everything inside a
    /// hole is part of the shape that surrounds it rather than a shape of its own.
    /// </summary>
    public const string RetrievalExternal = "external";

    /// <summary>
    /// The retrieval option that reports every contour, including the ones inside a
    /// hole, with no promise about which contour encloses which.
    /// </summary>
    public const string RetrievalList = "list";

    /// <summary>The category of nodes that mark a shape on a frame.</summary>
    public const string DrawCategory = "Draw";

    /// <summary>The category of nodes that reshape a frame by a structuring element.</summary>
    public const string MorphologyCategory = "Morphology";

    /// <summary>The category of nodes that bring images into a workflow or write them out of it.</summary>
    public const string InputOutputCategory = "Input/Output";

    /// <summary>The category of nodes that change the geometry of a frame.</summary>
    public const string TransformCategory = "Transform";

    /// <summary>The category of nodes that filter a frame.</summary>
    public const string FilterCategory = "Filter";

    /// <summary>The category of nodes that separate a frame into foreground and background.</summary>
    public const string ThresholdCategory = "Threshold";

    /// <summary>The category of nodes that find or draw the boundary of a shape.</summary>
    public const string ContoursCategory = "Contours";

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

    /// <summary>The shapes of structuring element a morphology node accepts, in the order the editor shows them.</summary>
    public static IReadOnlyList<string> KernelShapeOptions { get; } =
    [
        KernelShapeRect,
        KernelShapeEllipse,
        KernelShapeCross,
    ];

    /// <summary>The operations the morphology ex node accepts, in the order the editor shows them.</summary>
    public static IReadOnlyList<string> MorphologyOperationOptions { get; } =
    [
        MorphologyOpen,
        MorphologyClose,
        MorphologyGradient,
        MorphologyTopHat,
        MorphologyBlackHat,
    ];

    /// <summary>
    /// The contour sets the find contours node reports, in the order the editor shows
    /// them. The two modes that exist to report which contour encloses which are not
    /// offered: the value this node publishes is a flat list of contours, so a
    /// hierarchical mode would return what <see cref="RetrievalList"/> returns and call
    /// it something else.
    /// </summary>
    public static IReadOnlyList<string> RetrievalOptions { get; } =
    [
        RetrievalExternal,
        RetrievalList,
    ];
}
