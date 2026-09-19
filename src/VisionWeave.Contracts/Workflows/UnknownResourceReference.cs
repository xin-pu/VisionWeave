using System.Text.Json;

namespace VisionWeave.Contracts.Workflows;

/// <summary>
/// A resource entry of a kind this build does not model, kept exactly as the
/// document stored it so that a save never drops a reference this build cannot
/// interpret. A later build that knows the kind reads the fragment instead of
/// reconstructing it, which is the same reason an unknown document field is
/// preserved rather than discarded.
/// </summary>
public sealed record UnknownResourceReference : ResourceReference
{
    /// <summary>
    /// Creates the reference from the fragment the document stored.
    /// </summary>
    /// <param name="kind">
    /// The kind the entry declared, or <see langword="null"/> when it declared none.
    /// </param>
    /// <param name="json">The stored fragment, which is one complete JSON value.</param>
    /// <exception cref="ArgumentException">
    /// The fragment is not one complete JSON value, so no writer may be handed it.
    /// </exception>
    public UnknownResourceReference(string? kind, string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (!IsCompleteJsonValue(json))
        {
            throw new ArgumentException(
                "A preserved resource fragment must be one complete JSON value.",
                nameof(json));
        }

        Kind = kind;
        Json = json;
    }

    /// <summary>Gets the kind the entry declared, when it declared one.</summary>
    public string? Kind { get; }

    /// <summary>
    /// Gets the stored fragment. It is validated when the reference is created, so
    /// a writer can emit it without re-reading it first.
    /// </summary>
    public string Json { get; }

    private static bool IsCompleteJsonValue(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
