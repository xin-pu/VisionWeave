namespace VisionWeave.OpenCv.Nodes;

/// <summary>
/// The accepted ranges of the OpenCV node parameters. The definitions declare
/// them to the editor and the executors validate against them, so both read the
/// same numbers instead of keeping two copies that drift apart.
/// </summary>
public static class OpenCvParameterBounds
{
    /// <summary>The smallest kernel dimension a smoothing node accepts. One passes the frame through unchanged.</summary>
    public const int MinKernelSize = 1;

    /// <summary>The greatest kernel dimension a smoothing node accepts.</summary>
    public const int MaxKernelSize = 99;

    /// <summary>The smallest sigma a smoothing node accepts. Zero lets OpenCV choose one.</summary>
    public const double MinSigma = 0d;

    /// <summary>The greatest Gaussian sigma the node accepts.</summary>
    public const double MaxSigma = 100d;

    /// <summary>The greatest bilateral sigma, which is the whole range of an 8-bit intensity.</summary>
    public const double MaxBilateralSigma = 255d;

    /// <summary>The smallest frame dimension the resize node produces.</summary>
    public const int MinDimension = 1;

    /// <summary>The greatest frame dimension the resize node produces.</summary>
    public const int MaxDimension = 16384;

    /// <summary>The smallest median kernel dimension. A median of one pixel would smooth nothing.</summary>
    public const int MinMedianKernelSize = 3;

    /// <summary>The greatest derivative kernel dimension the node offers, which is the widest one OpenCV documents.</summary>
    public const int MaxDerivativeKernelSize = 7;

    /// <summary>The lowest derivative order a derivative node applies.</summary>
    public const int MinDerivativeOrder = 0;

    /// <summary>The highest derivative order a derivative node applies, which is what a three by three kernel reaches.</summary>
    public const int MaxDerivativeOrder = 2;

    /// <summary>The smallest factor a derivative node applies to a result.</summary>
    public const double MinScale = 0d;

    /// <summary>The greatest factor a derivative node applies to a result.</summary>
    public const double MaxScale = 100d;

    /// <summary>The smallest aperture the edge node measures a gradient with.</summary>
    public const int MinCannyApertureSize = 3;

    /// <summary>The greatest aperture the edge node measures a gradient with.</summary>
    public const int MaxCannyApertureSize = 7;

    /// <summary>
    /// The greatest gradient an 8-bit frame can produce, which is what an edge
    /// threshold is compared against: two differences of 255, which is the widest
    /// step between two neighbouring pixels.
    /// </summary>
    public const double MaxEdgeThreshold = 2 * MaxLevel;

    /// <summary>The smallest neighbourhood an adaptive threshold can compare a pixel against.</summary>
    public const int MinBlockSize = 3;

    /// <summary>The smallest accepted rectangle coordinate.</summary>
    public const int MinOrigin = 0;

    /// <summary>The smallest accepted threshold, which is also the darkest 8-bit pixel.</summary>
    public const double MinLevel = 0d;

    /// <summary>The greatest accepted threshold, which is also the brightest 8-bit pixel.</summary>
    public const double MaxLevel = 255d;

    /// <summary>The smallest offset an adaptive threshold subtracts from the local average.</summary>
    public const double MinConstant = -MaxLevel;

    /// <summary>The greatest offset an adaptive threshold subtracts from the local average.</summary>
    public const double MaxConstant = MaxLevel;
}
