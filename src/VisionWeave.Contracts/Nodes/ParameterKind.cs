namespace VisionWeave.Contracts.Nodes;

/// <summary>
/// Describes the editor and validation behavior of a node parameter.
/// </summary>
public enum ParameterKind
{
    /// <summary>A floating point number, optionally bounded.</summary>
    Number,

    /// <summary>A whole number, optionally bounded.</summary>
    Integer,

    /// <summary>A boolean switch.</summary>
    Boolean,

    /// <summary>Free text.</summary>
    Text,

    /// <summary>One value from a declared set of options.</summary>
    Option,

    /// <summary>A file or directory path.</summary>
    Path,
}
