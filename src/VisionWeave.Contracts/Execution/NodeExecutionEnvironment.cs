namespace VisionWeave.Contracts.Execution;

/// <summary>
/// The non-secret environment one run gives every node: the directory a relative
/// file path is resolved against, so a node that reads or writes a file never
/// depends on where the process happens to have been started (ADR-0012).
/// <para>
/// A run that touches no file needs no working directory, and an environment
/// without one refuses every file access rather than falling back to the process
/// directory, which is why <see cref="Default"/> exists and is empty.
/// </para>
/// </summary>
public sealed record NodeExecutionEnvironment
{
    /// <summary>
    /// Gets the environment of a run that resolves no file path.
    /// </summary>
    public static NodeExecutionEnvironment Default { get; } = new();

    /// <summary>
    /// Gets the absolute directory a relative path is resolved against, when the
    /// run has one.
    /// </summary>
    public string? WorkingDirectory { get; private init; }

    /// <summary>
    /// Creates the environment of a run whose files live beside its document.
    /// </summary>
    /// <param name="workingDirectory">The absolute directory of the document being run.</param>
    /// <returns>The environment.</returns>
    /// <exception cref="ArgumentException">The directory is null, empty, or blank.</exception>
    public static NodeExecutionEnvironment At(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        return new NodeExecutionEnvironment { WorkingDirectory = workingDirectory };
    }
}
