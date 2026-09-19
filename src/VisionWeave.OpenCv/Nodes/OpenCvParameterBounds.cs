namespace VisionWeave.OpenCv.Nodes;

/// <summary>
/// The accepted ranges of the OpenCV node parameters. The definitions declare
/// them to the editor and the executors validate against them, so both read the
/// same numbers instead of keeping two copies that drift apart.
/// </summary>
public static class OpenCvParameterBounds
{
    /// <summary>The smallest Gaussian kernel dimension. A kernel is odd, so 1 disables smoothing.</summary>
    public const int MinKernelSize = 1;

    /// <summary>The greatest Gaussian kernel dimension the node accepts.</summary>
    public const int MaxKernelSize = 99;

    /// <summary>The smallest Gaussian sigma. Zero lets OpenCV derive it from the kernel.</summary>
    public const double MinSigma = 0d;

    /// <summary>The greatest Gaussian sigma the node accepts.</summary>
    public const double MaxSigma = 100d;

    /// <summary>The smallest frame dimension the resize node produces.</summary>
    public const int MinDimension = 1;

    /// <summary>The greatest frame dimension the resize node produces.</summary>
    public const int MaxDimension = 16384;

    /// <summary>The smallest median kernel dimension. A median of one pixel would smooth nothing.</summary>
    public const int MinMedianKernelSize = 3;

    /// <summary>The smallest accepted rectangle coordinate.</summary>
    public const int MinOrigin = 0;

    /// <summary>The smallest accepted threshold, which is also the darkest 8-bit pixel.</summary>
    public const double MinLevel = 0d;

    /// <summary>The greatest accepted threshold, which is also the brightest 8-bit pixel.</summary>
    public const double MaxLevel = 255d;
}
