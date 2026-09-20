namespace VisionWeave.OpenCv.Nodes;

/// <summary>
/// The words the OpenCV nodes can be filtered by, declared once for the provider so
/// two nodes cannot spell the same idea two ways: one "filter" and one "filtering"
/// would be two filters that each show half the answer. A definition names the words
/// it carries out of this set, in the same way it names one of the declared
/// categories, and a node family that arrives with a word the set does not hold
/// extends the set deliberately rather than by typo.
/// <para>
/// The words are declared in the order they sort, which is the order the catalogue
/// offers them, so adding one here places its chip where a reader expects it.
/// </para>
/// </summary>
public static class OpenCvNodeTags
{
    /// <summary>A node that reads or writes the channels a pixel is made of.</summary>
    public const string Colour = "colour";

    /// <summary>A node that measures how fast an image changes, or where it changes fastest.</summary>
    public const string Edges = "edges";

    /// <summary>A node that reads or writes a file, which is how a workflow starts and ends.</summary>
    public const string File = "file";

    /// <summary>A node that changes where the pixels are rather than what they say.</summary>
    public const string Geometry = "geometry";

    /// <summary>
    /// A node that marks a frame by hand rather than by measuring it. It is not the
    /// same word as <see cref="Mask"/>: drawing writes into a frame, and a mask is
    /// what a decision about a pixel was written into.
    /// </summary>
    public const string Marking = "marking";

    /// <summary>
    /// A node that works on the black-and-white image a decision is expressed in: the
    /// one a threshold or an edge map produces, and the one a hand-drawn mark writes
    /// into. It is the word no category carries, and the reason the tag axis exists.
    /// </summary>
    public const string Mask = "mask";

    /// <summary>A node that reshapes a region by the shape of a structuring element.</summary>
    public const string Morphology = "morphology";

    /// <summary>A node that averages a pixel with its neighbourhood, which is how noise is removed.</summary>
    public const string Smoothing = "smoothing";

    /// <summary>A node that decides each pixel against a level or its own neighbourhood.</summary>
    public const string Threshold = "threshold";

    /// <summary>Gets every word the provider publishes, in the order the catalogue offers them.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Colour,
        Edges,
        File,
        Geometry,
        Marking,
        Mask,
        Morphology,
        Smoothing,
        Threshold,
    ];
}
