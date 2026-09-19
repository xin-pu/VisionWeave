using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Validation;

/// <summary>
/// The outcome of validating one workflow document: the diagnostics that were
/// reported, in a deterministic order, and whether any of them blocks execution.
/// A document stays editable and savable while invalid; only execution requires
/// a result with no error-severity diagnostic.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Initializes a result from the reported diagnostics.
    /// </summary>
    /// <param name="diagnostics">The reported diagnostics.</param>
    public ValidationResult(IEnumerable<NodeDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = [.. diagnostics];
    }

    /// <summary>
    /// Gets a result that reports nothing.
    /// </summary>
    public static ValidationResult Valid { get; } = new([]);

    /// <summary>
    /// Gets the reported diagnostics.
    /// </summary>
    public IReadOnlyList<NodeDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the diagnostics that block execution.
    /// </summary>
    public IReadOnlyList<NodeDiagnostic> Errors
        => [.. Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

    /// <summary>
    /// Gets a value indicating whether the document can be executed.
    /// </summary>
    public bool IsValid => !Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Determines whether a diagnostic with the given code was reported.
    /// </summary>
    /// <param name="code">A stable code from <see cref="DiagnosticCodes"/>.</param>
    /// <returns><see langword="true"/> when the code was reported.</returns>
    public bool HasCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return Diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, code, StringComparison.Ordinal));
    }
}
