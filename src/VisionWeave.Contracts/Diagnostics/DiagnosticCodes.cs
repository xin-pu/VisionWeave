namespace VisionWeave.Contracts.Diagnostics;

/// <summary>
/// Declares the stable diagnostic identifiers shared by validation, execution,
/// tests, and the UI. Production code and consumers reference these identifiers
/// instead of duplicating their string values.
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>The graph contains a cycle or otherwise violates structural rules.</summary>
    public const string InvalidGraph = "VW-GRAPH-001";

    /// <summary>The connection is rejected by direction, type, or multiplicity rules.</summary>
    public const string IncompatiblePort = "VW-PORT-001";

    /// <summary>A saved connection refers to a port that no longer exists.</summary>
    public const string UnknownPort = "VW-PORT-002";

    /// <summary>The node type is not present in the resolved catalog.</summary>
    public const string MissingNodeDefinition = "VW-NODE-001";

    /// <summary>The node type is known but its saved definition version cannot be migrated.</summary>
    public const string UnsupportedNodeVersion = "VW-NODE-002";

    /// <summary>The node executor reported a failure.</summary>
    public const string NodeExecutionFailed = "VW-EXEC-001";

    /// <summary>The node execution observed cancellation.</summary>
    public const string NodeExecutionCancelled = "VW-EXEC-002";

    /// <summary>The node was not scheduled because an upstream node failed.</summary>
    public const string NodeExecutionBlocked = "VW-EXEC-003";

    /// <summary>An image frame lease was still outstanding when the run ended.</summary>
    public const string LeaseLeaked = "VW-EXEC-004";

    /// <summary>An executor ignored cancellation beyond the configured grace period.</summary>
    public const string ExecutorIgnoredCancellation = "VW-EXEC-005";

    /// <summary>An output was too large for the cache budget and was not cached.</summary>
    public const string OutputExceedsCacheBudget = "VW-EXEC-006";

    /// <summary>No executor is registered for the definition's executor identifier.</summary>
    public const string MissingExecutor = "VW-EXEC-007";

    /// <summary>A published output could not be converted for preview.</summary>
    public const string PreviewConversionFailed = "VW-EXEC-008";

    /// <summary>A private resource of a node could not be released.</summary>
    public const string ResourceDisposalFailed = "VW-EXEC-009";
}
