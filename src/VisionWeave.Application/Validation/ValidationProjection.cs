using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Validation;

/// <summary>
/// An immutable, revision-tagged view of one validation run, indexed by stable
/// node instance identifier. Selection is what changes when the user clicks, so
/// a projection answers a selection query without re-validating the document and
/// without holding a view model: a selection that outlives the edit which
/// produced the projection reports nothing for a node the document no longer
/// contains instead of throwing or keeping a stale badge alive, and the caller
/// can tell a current projection from a superseded one through
/// <see cref="Matches"/>.
/// </summary>
public sealed class ValidationProjection
{
    private readonly Dictionary<Guid, List<NodeDiagnostic>> _byNode;
    private readonly List<NodeDiagnostic> _documentDiagnostics;
    private readonly int _errorCount;

    /// <summary>
    /// Initializes a projection from a validation outcome and the document
    /// revision it was computed from.
    /// </summary>
    /// <param name="result">The validation outcome to index.</param>
    /// <param name="documentRevision">
    /// The revision of the document the outcome describes. A layout-only edit
    /// leaves it unchanged, which is why a projection stays current across a
    /// node move.
    /// </param>
    public ValidationProjection(ValidationResult result, long documentRevision)
    {
        ArgumentNullException.ThrowIfNull(result);

        DocumentRevision = documentRevision;
        Diagnostics = result.Diagnostics;
        _byNode = [];
        _documentDiagnostics = [];

        // The validator emits a deterministic sequence, and grouping preserves
        // it, so a node's diagnostics and a selection's diagnostics keep that
        // same order instead of depending on how they were grouped.
        foreach (NodeDiagnostic diagnostic in Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                _errorCount++;
            }

            if (diagnostic.NodeInstanceId is not { } nodeInstanceId)
            {
                _documentDiagnostics.Add(diagnostic);
                continue;
            }

            if (!_byNode.TryGetValue(nodeInstanceId, out List<NodeDiagnostic>? nodeDiagnostics))
            {
                nodeDiagnostics = [];
                _byNode.Add(nodeInstanceId, nodeDiagnostics);
            }

            nodeDiagnostics.Add(diagnostic);
        }
    }

    /// <summary>
    /// Gets the revision of the document this projection describes.
    /// </summary>
    public long DocumentRevision { get; }

    /// <summary>
    /// Gets the reported diagnostics, in the order validation reported them.
    /// </summary>
    public IReadOnlyList<NodeDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets a value indicating whether the document can be executed.
    /// </summary>
    public bool IsValid => _errorCount == 0;

    /// <summary>
    /// Gets the diagnostics that belong to the document rather than to one node.
    /// </summary>
    public IReadOnlyList<NodeDiagnostic> DocumentDiagnostics => _documentDiagnostics;

    /// <summary>
    /// Determines whether this projection still describes the given document
    /// revision. A projection that does not match must be replaced rather than
    /// queried, because its answers describe an earlier document.
    /// </summary>
    /// <param name="documentRevision">The revision to compare against.</param>
    /// <returns><see langword="true"/> when the projection is current.</returns>
    public bool Matches(long documentRevision) => DocumentRevision == documentRevision;

    /// <summary>
    /// Gets the diagnostics reported for one node instance.
    /// </summary>
    /// <param name="nodeInstanceId">The node instance to report on.</param>
    /// <returns>
    /// The node's diagnostics in validation order, or an empty list when the
    /// node is clean or the projection never saw it.
    /// </returns>
    public IReadOnlyList<NodeDiagnostic> DiagnosticsFor(Guid nodeInstanceId)
        => _byNode.TryGetValue(nodeInstanceId, out List<NodeDiagnostic>? diagnostics) ? diagnostics : [];

    /// <summary>
    /// Gets the severity a node should present, which is its highest one.
    /// </summary>
    /// <param name="nodeInstanceId">The node instance to report on.</param>
    /// <returns>
    /// The node's highest severity, or <see langword="null"/> when the node is
    /// clean or the projection never saw it.
    /// </returns>
    public DiagnosticSeverity? SeverityOf(Guid nodeInstanceId)
        => _byNode.TryGetValue(nodeInstanceId, out List<NodeDiagnostic>? diagnostics)
            ? HighestSeverity(diagnostics)
            : null;

    /// <summary>
    /// Reports the diagnostics that belong to a selection, which is the union of
    /// the selected nodes' diagnostics and the document-level ones. A selection
    /// panel that showed only the selected nodes would claim a selection is
    /// runnable while a document-level failure still blocks the run, and it must
    /// not depend on the order or the multiplicity of the identifiers it is
    /// given.
    /// </summary>
    /// <param name="nodeInstanceIds">
    /// The selected node instances, which may include identifiers the document
    /// no longer contains or never contained.
    /// </param>
    /// <returns>The selection's validation state.</returns>
    public SelectionValidation Select(IEnumerable<Guid> nodeInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(nodeInstanceIds);

        HashSet<Guid> selected = [.. nodeInstanceIds];

        // Document-level diagnostics come first so a panel shows the graph-wide
        // reason before the per-node detail.
        List<NodeDiagnostic> diagnostics = [.. _documentDiagnostics];

        foreach (NodeDiagnostic diagnostic in Diagnostics)
        {
            if (diagnostic.NodeInstanceId is { } nodeInstanceId && selected.Contains(nodeInstanceId))
            {
                diagnostics.Add(diagnostic);
            }
        }

        return new SelectionValidation(diagnostics);
    }

    private static DiagnosticSeverity? HighestSeverity(IReadOnlyList<NodeDiagnostic> diagnostics)
    {
        DiagnosticSeverity? highest = null;

        foreach (NodeDiagnostic diagnostic in diagnostics)
        {
            if (highest is not { } current || diagnostic.Severity > current)
            {
                highest = diagnostic.Severity;
            }
        }

        return highest;
    }
}
