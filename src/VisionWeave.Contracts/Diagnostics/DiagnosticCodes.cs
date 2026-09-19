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

    /// <summary>A successful node reported a value on a port its definition does not declare as an output.</summary>
    public const string NodeOutputUndeclared = "VW-EXEC-010";

    /// <summary>A reported output value does not carry the port type its port declares.</summary>
    public const string NodeOutputTypeMismatch = "VW-EXEC-011";

    /// <summary>A node reported one image frame lease on more than one output port.</summary>
    public const string NodeOutputLeaseAliased = "VW-EXEC-012";

    /// <summary>A node reported an image frame lease it received as an input.</summary>
    public const string NodeOutputLeaseNotOwned = "VW-EXEC-013";

    /// <summary>A saved parameter value is not the shape its definition declares.</summary>
    public const string InvalidParameterValue = "VW-PARAM-001";

    /// <summary>A saved numeric parameter value is outside the declared bounds.</summary>
    public const string ParameterOutOfRange = "VW-PARAM-002";

    /// <summary>A saved option value is not one the definition declares.</summary>
    public const string ParameterOptionNotDeclared = "VW-PARAM-003";

    /// <summary>The document saves a parameter the node definition does not declare.</summary>
    public const string UnknownParameter = "VW-PARAM-004";

    /// <summary>A required parameter has neither a saved value nor a declared default.</summary>
    public const string MissingRequiredParameter = "VW-PARAM-005";

    /// <summary>The stored file is not a readable workflow document.</summary>
    public const string UnreadableDocument = "VW-FILE-001";

    /// <summary>The stored document schema is not one this build can migrate, so it opens read-only.</summary>
    public const string UnsupportedDocumentSchema = "VW-FILE-002";

    /// <summary>A stored entry or field was incoherent and was skipped while loading.</summary>
    public const string DroppedDocumentEntry = "VW-FILE-003";
}
