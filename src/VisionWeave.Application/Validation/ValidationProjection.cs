using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.Application.Validation;

/// <summary>
/// An immutable, revision-tagged view of one validation run, indexed by stable
/// node instance identifier and by the narrower element a diagnostic names.
/// Selection is what changes when the user clicks, so a projection answers a
/// selection query without re-validating the document and without holding a view
/// model: a selection that outlives the edit which produced the projection
/// reports nothing for a node the document no longer contains instead of throwing
/// or keeping a stale badge alive, and the caller can tell a current projection
/// from a superseded one through <see cref="Matches"/>.
/// </summary>
/// <remarks>
/// A port, a parameter, and a connection are indexed under the identifiers the
/// diagnostic names, so a port shows the conditions of that port rather than the
/// severity of the node that owns it. A diagnostic is never lost to indexing: one
/// whose target this projection does not index stays under the node it names, or
/// under the document when it names none.
/// </remarks>
public sealed class ValidationProjection
{
    private readonly Dictionary<Guid, List<NodeDiagnostic>> _byNode;
    private readonly Dictionary<Guid, List<NodeDiagnostic>> _nodeLevel;
    private readonly Dictionary<(Guid NodeId, string PortId), List<NodeDiagnostic>> _byPort;
    private readonly Dictionary<(Guid NodeId, string ParameterName), List<NodeDiagnostic>> _byParameter;
    private readonly Dictionary<Guid, List<NodeDiagnostic>> _byConnection;
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
        _nodeLevel = [];
        _byPort = [];
        _byParameter = [];
        _byConnection = [];
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
                Index(diagnostic);
            }
            else
            {
                Group(_byNode, nodeInstanceId).Add(diagnostic);

                if (!Index(diagnostic))
                {
                    // A condition this projection could not attribute to a
                    // narrower element is reported at the node scope it names, so
                    // the node's ports and parameters present it instead of
                    // hiding a condition that must not be lost. A condition that
                    // names no node at all stays a document diagnostic.
                    Group(_nodeLevel, nodeInstanceId).Add(diagnostic);
                }
            }
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
    /// Gets the diagnostics that apply to one port: the conditions the port
    /// itself earned, preceded by the conditions the node reports about itself.
    /// A port does not inherit the node's document-level or other-member
    /// diagnostics, so a parameter outside its range no longer marks every
    /// connector of the same node as invalid.
    /// </summary>
    /// <param name="nodeInstanceId">The node instance that owns the port.</param>
    /// <param name="portId">The port identifier.</param>
    /// <returns>
    /// The applicable diagnostics in validation order. A port the projection
    /// never saw answers with its node's own conditions, and a node it never saw
    /// answers with nothing, so a view bound to a removed port degrades to the
    /// node instead of claiming the port is clean.
    /// </returns>
    public IReadOnlyList<NodeDiagnostic> DiagnosticsForPort(Guid nodeInstanceId, string portId)
        => ForOwnedElement(_byPort, (nodeInstanceId, portId), nodeInstanceId);

    /// <summary>
    /// Gets the severity a port should present.
    /// </summary>
    /// <param name="nodeInstanceId">The node instance that owns the port.</param>
    /// <param name="portId">The port identifier.</param>
    /// <returns>The highest applicable severity, or <see langword="null"/>.</returns>
    public DiagnosticSeverity? SeverityOfPort(Guid nodeInstanceId, string portId)
        => HighestSeverity(DiagnosticsForPort(nodeInstanceId, portId));

    /// <summary>
    /// Gets the diagnostics that apply to one parameter: the conditions the
    /// parameter itself earned, preceded by the conditions the node reports about
    /// itself. A node-level failure is what makes a parameter's own state
    /// unknowable, which is why it is reported together with the parameter's
    /// conditions and not instead away from them.
    /// </summary>
    /// <param name="nodeInstanceId">The node instance that owns the parameter.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The applicable diagnostics in validation order.</returns>
    public IReadOnlyList<NodeDiagnostic> DiagnosticsForParameter(Guid nodeInstanceId, string parameterName)
        => ForOwnedElement(_byParameter, (nodeInstanceId, parameterName), nodeInstanceId);

    /// <summary>
    /// Gets the severity a parameter should present.
    /// </summary>
    /// <param name="nodeInstanceId">The node instance that owns the parameter.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The highest applicable severity, or <see langword="null"/>.</returns>
    public DiagnosticSeverity? SeverityOfParameter(Guid nodeInstanceId, string parameterName)
        => HighestSeverity(DiagnosticsForParameter(nodeInstanceId, parameterName));

    /// <summary>
    /// Gets the diagnostics that belong to one connection. A connection belongs
    /// to two nodes, so it inherits neither node's conditions, and a condition
    /// the wire caused is reported here whether or not both of its ends still
    /// name a node the document contains.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <returns>
    /// The connection's diagnostics in validation order, or an empty list when
    /// the connection is clean or the projection never saw it.
    /// </returns>
    public IReadOnlyList<NodeDiagnostic> DiagnosticsForConnection(Guid connectionId)
        => _byConnection.TryGetValue(connectionId, out List<NodeDiagnostic>? diagnostics) ? diagnostics : [];

    /// <summary>
    /// Gets the severity a connection should present.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <returns>The connection's highest severity, or <see langword="null"/>.</returns>
    public DiagnosticSeverity? SeverityOfConnection(Guid connectionId)
        => HighestSeverity(DiagnosticsForConnection(connectionId));

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

    private IReadOnlyList<NodeDiagnostic> ForOwnedElement<TKey>(
        IReadOnlyDictionary<TKey, List<NodeDiagnostic>> index,
        TKey key,
        Guid ownerNodeId)
        where TKey : notnull
    {
        bool owned = _nodeLevel.TryGetValue(ownerNodeId, out List<NodeDiagnostic>? nodeDiagnostics);

        if (!index.TryGetValue(key, out List<NodeDiagnostic>? own))
        {
            // The element this caller asked about is not in this projection: the
            // document has moved on, or it was never reported. The node's own
            // conditions are the nearest broader answer, and there are none when
            // the node is unknown too, so a stale view shows nothing rather than
            // a badge that describes an element it can no longer name.
            return owned ? nodeDiagnostics! : [];
        }

        if (!owned)
        {
            return own;
        }

        // The broader scope comes first, so a reader learns why the element
        // cannot be trusted before what is wrong with it in particular.
        return [.. nodeDiagnostics!, .. own];
    }

    private bool Index(NodeDiagnostic diagnostic)
    {
        switch (diagnostic.Target)
        {
            case PortTarget port when !string.IsNullOrEmpty(port.PortId):
                Group(_byPort, (port.NodeInstanceId, port.PortId)).Add(diagnostic);
                return true;
            case ParameterTarget parameter when !string.IsNullOrEmpty(parameter.ParameterName):
                Group(_byParameter, (parameter.NodeInstanceId, parameter.ParameterName)).Add(diagnostic);
                return true;
            case ConnectionTarget connection:
                Group(_byConnection, connection.ConnectionId).Add(diagnostic);
                return true;
            default:
                // A diagnostic that names no target, one whose identifier cannot
                // be used, and one whose target kind a later contract version
                // adds are all left to the scope they already have, where they
                // remain visible instead of vanishing from every view.
                return false;
        }
    }

    private static List<NodeDiagnostic> Group<TKey>(
        Dictionary<TKey, List<NodeDiagnostic>> index,
        TKey key)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out List<NodeDiagnostic>? diagnostics))
        {
            diagnostics = [];
            index.Add(key, diagnostics);
        }

        return diagnostics;
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
