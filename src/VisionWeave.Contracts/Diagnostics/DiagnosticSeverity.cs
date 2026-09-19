namespace VisionWeave.Contracts.Diagnostics;

/// <summary>
/// Declares the severity of a diagnostic reported by validation or execution.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>Informational context, such as a cache decision.</summary>
    Information,

    /// <summary>The workflow can run, but attention is warranted.</summary>
    Warning,

    /// <summary>The affected node or graph cannot be used as requested.</summary>
    Error,
}
