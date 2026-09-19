namespace VisionWeave.Contracts.Workflows;

/// <summary>
/// A reference to a file a node reads or writes. The path travels as the document
/// recorded it; the optional digest is the one a user supplied, not one this
/// build computed.
/// </summary>
public sealed record FileResourceReference : ResourceReference
{
    /// <summary>
    /// Creates the reference.
    /// </summary>
    /// <param name="path">The path the document records.</param>
    /// <param name="expectedSha256">
    /// The digest the user supplied, or <see langword="null"/> when the document
    /// pins nothing.
    /// </param>
    /// <exception cref="ArgumentException">The path or the digest is empty or whitespace.</exception>
    public FileResourceReference(string path, string? expectedSha256 = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (expectedSha256 is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);
        }

        Path = path;
        ExpectedSha256 = expectedSha256;
    }

    /// <summary>Gets the path the document records.</summary>
    public string Path { get; }

    /// <summary>Gets the digest the user supplied, when the document pins one.</summary>
    public string? ExpectedSha256 { get; }
}
