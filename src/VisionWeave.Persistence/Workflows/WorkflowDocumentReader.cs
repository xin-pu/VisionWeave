using System.Globalization;
using System.Text.Json;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Persistence.Workflows;

/// <summary>
/// Reads a stored workflow document. Reading checks the file's JSON and schema
/// integrity, preserves everything this build does not model, and yields a
/// document whenever the file is coherent enough to display. Resolving node
/// definitions, and deciding whether a document may execute, belongs to the
/// application validator; a node whose type is unknown therefore loads as it was
/// saved and is reported there.
/// </summary>
/// <remarks>
/// <para>
/// The load policy is explicit rather than implicit:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A file that is not JSON, or that is missing its identifier, name, creation
/// instant, or revision, cannot be represented and reports <c>VW-FILE-001</c>
/// without a document.
/// </description></item>
/// <item><description>
/// A document whose schema version is not the current one opens read-only and
/// reports <c>VW-FILE-002</c>, because this build applies no migration.
/// </description></item>
/// <item><description>
/// A node or connection entry that cannot be represented is skipped with
/// <c>VW-FILE-003</c> instead of failing the whole load.
/// </description></item>
/// <item><description>
/// Unknown document fields are preserved as top-level fields, unknown node
/// fields inside the node's extension data, and a node entry without a
/// <c>typeVersion</c> is treated as version 0.
/// </description></item>
/// </list>
/// </remarks>
public static class WorkflowDocumentReader
{
    private static readonly HashSet<string> DocumentFields = new(StringComparer.Ordinal)
    {
        WorkflowJson.SchemaVersion,
        WorkflowJson.DocumentId,
        WorkflowJson.Name,
        WorkflowJson.CreatedUtc,
        WorkflowJson.ModifiedUtc,
        WorkflowJson.AppVersion,
        WorkflowJson.Revision,
        WorkflowJson.RequiredPlugins,
        WorkflowJson.Extensions,
        WorkflowJson.Nodes,
        WorkflowJson.Connections,
    };

    private static readonly HashSet<string> NodeFields = new(StringComparer.Ordinal)
    {
        WorkflowJson.NodeId,
        WorkflowJson.NodeTypeId,
        WorkflowJson.NodeTypeVersion,
        WorkflowJson.NodeParameters,
        WorkflowJson.NodeExtensionData,
        WorkflowJson.NodeLayout,
        WorkflowJson.NodeLabel,
        WorkflowJson.NodeEnabled,
    };

    /// <summary>
    /// Reads a document from a stream.
    /// </summary>
    /// <param name="source">The stream that holds the document.</param>
    /// <returns>The outcome of the read.</returns>
    public static WorkflowLoadResult Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(source);
        }
        catch (JsonException exception)
        {
            return Unreadable("The workflow file is not valid JSON.", exception);
        }

        using (json)
        {
            return Read(json.RootElement);
        }
    }

    /// <summary>
    /// Reads a document from a file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The outcome of the read.</returns>
    public static WorkflowLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using FileStream stream = new(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream);
    }

    private static WorkflowLoadResult Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Unreadable("The workflow file must contain a JSON object.");
        }

        int schemaVersion = ReadSchemaVersion(root);
        List<NodeDiagnostic> diagnostics = [];

        if (schemaVersion != WorkflowFileFormat.CurrentSchemaVersion)
        {
            diagnostics.Add(new NodeDiagnostic(
                DiagnosticCodes.UnsupportedDocumentSchema,
                DiagnosticSeverity.Warning,
                $"Schema version {schemaVersion} is not supported by this build, so the document opens read-only.",
                null));
        }

        if (!TryReadGuid(root, WorkflowJson.DocumentId, out Guid documentId)
            || !TryReadText(root, WorkflowJson.Name, out string? name)
            || !TryReadInstant(root, WorkflowJson.CreatedUtc, out DateTimeOffset createdUtc)
            || !TryReadRevision(root, out long revision))
        {
            return Unreadable(
                "The workflow file must declare documentId, name, createdUtc, and revision.");
        }

        DateTimeOffset modifiedUtc = TryReadInstant(root, WorkflowJson.ModifiedUtc, out DateTimeOffset stored)
            ? stored
            : createdUtc;

        WorkflowDocument document = WorkflowDocument.Hydrate(
            documentId,
            name!,
            createdUtc,
            modifiedUtc,
            revision,
            target => Fill(target, root, diagnostics));

        return new WorkflowLoadResult(
            document,
            schemaVersion,
            schemaVersion != WorkflowFileFormat.CurrentSchemaVersion,
            diagnostics);
    }

    private static void Fill(WorkflowDocument target, JsonElement root, List<NodeDiagnostic> diagnostics)
    {
        ReadNodes(target, root, diagnostics);
        ReadConnections(target, root, diagnostics);

        foreach (string plugin in ReadTextArray(root, WorkflowJson.RequiredPlugins, diagnostics))
        {
            target.RequirePlugin(plugin);
        }

        if (TryReadText(root, WorkflowJson.AppVersion, out string? appVersion))
        {
            target.RecordAppVersion(appVersion);
        }

        PreserveDocumentFields(target, root, diagnostics);
    }

    private static void ReadNodes(WorkflowDocument target, JsonElement root, List<NodeDiagnostic> diagnostics)
    {
        if (!TryGetArray(root, WorkflowJson.Nodes, out JsonElement nodes, diagnostics))
        {
            return;
        }

        foreach (JsonElement entry in nodes.EnumerateArray())
        {
            ReadNode(target, entry, diagnostics);
        }
    }

    private static void ReadNode(WorkflowDocument target, JsonElement entry, List<NodeDiagnostic> diagnostics)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !TryReadGuid(entry, WorkflowJson.NodeId, out Guid instanceId)
            || !TryReadText(entry, WorkflowJson.NodeTypeId, out string? typeId))
        {
            Skip(diagnostics, null, "A node entry has no identifier or node type and was skipped.");
            return;
        }

        if (target.TryGetNode(instanceId, out _))
        {
            Skip(diagnostics, instanceId, $"A node entry repeats instance '{instanceId}' and was skipped.");
            return;
        }

        NodeInstance node = target.AddNode(
            new NodeTypeId(typeId!),
            ReadTypeVersion(entry),
            ReadPosition(entry),
            instanceId);

        if (TryReadText(entry, WorkflowJson.NodeLabel, out string? label))
        {
            target.SetNodeLabel(node.InstanceId, label);
        }

        if (entry.TryGetProperty(WorkflowJson.NodeEnabled, out JsonElement enabled)
            && enabled.ValueKind == JsonValueKind.False)
        {
            target.SetNodeEnabled(node.InstanceId, false);
        }

        ReadParameters(target, entry, node.InstanceId, diagnostics);
        PreserveNodeFields(target, entry, node.InstanceId, diagnostics);
    }

    private static void ReadParameters(
        WorkflowDocument target,
        JsonElement entry,
        Guid instanceId,
        List<NodeDiagnostic> diagnostics)
    {
        if (!entry.TryGetProperty(WorkflowJson.NodeParameters, out JsonElement parameters))
        {
            return;
        }

        if (parameters.ValueKind != JsonValueKind.Object)
        {
            Skip(diagnostics, instanceId, $"The parameters of node '{instanceId}' are not a JSON object and were skipped.");
            return;
        }

        foreach (JsonProperty parameter in parameters.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                Skip(diagnostics, instanceId, $"A parameter of node '{instanceId}' has no name and was skipped.");
                continue;
            }

            if (!WorkflowJson.TryReadValue(parameter.Value, out object? value))
            {
                Skip(
                    diagnostics,
                    instanceId,
                    $"Parameter '{parameter.Name}' of node '{instanceId}' holds a value the format does not store and was skipped.");
                continue;
            }

            target.SetNodeParameter(instanceId, parameter.Name, value);
        }
    }

    private static void PreserveNodeFields(
        WorkflowDocument target,
        JsonElement entry,
        Guid instanceId,
        List<NodeDiagnostic> diagnostics)
    {
        foreach (JsonProperty field in entry.EnumerateObject())
        {
            if (NodeFields.Contains(field.Name))
            {
                continue;
            }

            Preserve(target, instanceId, field, diagnostics);
        }

        if (!entry.TryGetProperty(WorkflowJson.NodeExtensionData, out JsonElement extensionData))
        {
            return;
        }

        if (extensionData.ValueKind != JsonValueKind.Object)
        {
            Skip(diagnostics, instanceId, $"The extension data of node '{instanceId}' is not a JSON object and was skipped.");
            return;
        }

        foreach (JsonProperty field in extensionData.EnumerateObject())
        {
            if (!string.IsNullOrWhiteSpace(field.Name))
            {
                target.PreserveNodeExtension(instanceId, field.Name, field.Value.GetRawText());
            }
        }
    }

    private static void PreserveDocumentFields(
        WorkflowDocument target,
        JsonElement root,
        List<NodeDiagnostic> diagnostics)
    {
        foreach (JsonProperty field in root.EnumerateObject())
        {
            if (DocumentFields.Contains(field.Name) || string.IsNullOrWhiteSpace(field.Name))
            {
                continue;
            }

            target.PreserveExtension(field.Name, field.Value.GetRawText());
        }

        if (!root.TryGetProperty(WorkflowJson.Extensions, out JsonElement extensions))
        {
            return;
        }

        if (extensions.ValueKind != JsonValueKind.Object)
        {
            Skip(diagnostics, null, "The extensions of the document are not a JSON object and were skipped.");
            return;
        }

        foreach (JsonProperty field in extensions.EnumerateObject())
        {
            if (!string.IsNullOrWhiteSpace(field.Name))
            {
                target.PreserveExtension(field.Name, field.Value.GetRawText());
            }
        }
    }

    private static void ReadConnections(WorkflowDocument target, JsonElement root, List<NodeDiagnostic> diagnostics)
    {
        if (!TryGetArray(root, WorkflowJson.Connections, out JsonElement connections, diagnostics))
        {
            return;
        }

        HashSet<Guid> seen = [];

        foreach (JsonElement entry in connections.EnumerateArray())
        {
            ReadConnection(target, entry, seen, diagnostics);
        }
    }

    private static void ReadConnection(
        WorkflowDocument target,
        JsonElement entry,
        HashSet<Guid> seen,
        List<NodeDiagnostic> diagnostics)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !TryReadGuid(entry, WorkflowJson.ConnectionId, out Guid connectionId)
            || !TryReadGuid(entry, WorkflowJson.ConnectionSourceNodeId, out Guid sourceNodeId)
            || !TryReadGuid(entry, WorkflowJson.ConnectionTargetNodeId, out Guid targetNodeId)
            || !TryReadText(entry, WorkflowJson.ConnectionSourcePortId, out string? sourcePortId)
            || !TryReadText(entry, WorkflowJson.ConnectionTargetPortId, out string? targetPortId))
        {
            Skip(diagnostics, null, "A connection entry is incomplete and was skipped.");
            return;
        }

        if (!seen.Add(connectionId))
        {
            Skip(diagnostics, null, $"A connection entry repeats connection '{connectionId}' and was skipped.");
            return;
        }

        if (!target.TryGetNode(sourceNodeId, out _) || !target.TryGetNode(targetNodeId, out _))
        {
            Skip(
                diagnostics,
                null,
                $"Connection '{connectionId}' refers to a node this document does not contain and was skipped.");
            return;
        }

        target.AddConnection(sourceNodeId, sourcePortId!, targetNodeId, targetPortId!, connectionId);
    }

    private static void Preserve(
        WorkflowDocument target,
        Guid instanceId,
        JsonProperty field,
        List<NodeDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(field.Name))
        {
            Skip(diagnostics, instanceId, $"A field of node '{instanceId}' has no name and was skipped.");
            return;
        }

        target.PreserveNodeExtension(instanceId, field.Name, field.Value.GetRawText());
    }

    private static int ReadSchemaVersion(JsonElement root)
        => root.TryGetProperty(WorkflowJson.SchemaVersion, out JsonElement version)
            && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out int declared)
                ? declared
                : WorkflowFileFormat.CurrentSchemaVersion;

    private static bool TryReadRevision(JsonElement root, out long revision)
    {
        revision = 0;

        if (!root.TryGetProperty(WorkflowJson.Revision, out JsonElement element)
            || element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt64(out long stored)
            || stored < 0)
        {
            return false;
        }

        revision = stored;
        return true;
    }

    private static int ReadTypeVersion(JsonElement entry)
    {
        if (entry.TryGetProperty(WorkflowJson.NodeTypeVersion, out JsonElement version)
            && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out int stored)
            && stored >= 0)
        {
            return stored;
        }

        return 0;
    }

    private static CanvasPosition ReadPosition(JsonElement entry)
    {
        if (!entry.TryGetProperty(WorkflowJson.NodeLayout, out JsonElement layout)
            || layout.ValueKind != JsonValueKind.Object)
        {
            return new CanvasPosition(0, 0);
        }

        return new CanvasPosition(ReadNumber(layout, WorkflowJson.LayoutX), ReadNumber(layout, WorkflowJson.LayoutY));
    }

    private static double ReadNumber(JsonElement parent, string name)
        => parent.TryGetProperty(name, out JsonElement element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out double value)
                ? value
                : 0;

    private static IReadOnlyList<string> ReadTextArray(
        JsonElement root,
        string name,
        List<NodeDiagnostic> diagnostics)
    {
        if (!TryGetArray(root, name, out JsonElement array, diagnostics))
        {
            return [];
        }

        List<string> values = [];

        foreach (JsonElement entry in array.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(entry.GetString()))
            {
                values.Add(entry.GetString()!);
            }
        }

        return values;
    }

    private static bool TryGetArray(
        JsonElement parent,
        string name,
        out JsonElement array,
        List<NodeDiagnostic> diagnostics)
    {
        array = default;

        if (!parent.TryGetProperty(name, out JsonElement element))
        {
            return false;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            Skip(diagnostics, null, $"The '{name}' field is not a JSON array and was skipped.");
            return false;
        }

        array = element;
        return true;
    }

    private static bool TryReadGuid(JsonElement parent, string name, out Guid value)
    {
        value = Guid.Empty;

        return parent.TryGetProperty(name, out JsonElement element)
            && element.ValueKind == JsonValueKind.String
            && Guid.TryParse(element.GetString(), out value)
            && value != Guid.Empty;
    }

    private static bool TryReadText(JsonElement parent, string name, out string? value)
    {
        value = null;

        if (!parent.TryGetProperty(name, out JsonElement element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadInstant(JsonElement parent, string name, out DateTimeOffset value)
    {
        value = default;

        return parent.TryGetProperty(name, out JsonElement element)
            && element.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                element.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out value);
    }

    private static void Skip(List<NodeDiagnostic> diagnostics, Guid? instanceId, string message)
        => diagnostics.Add(new NodeDiagnostic(
            DiagnosticCodes.DroppedDocumentEntry,
            DiagnosticSeverity.Error,
            message,
            instanceId));

    private static WorkflowLoadResult Unreadable(string message, Exception? exception = null)
        => new(
            null,
            WorkflowFileFormat.CurrentSchemaVersion,
            IsReadOnly: true,
            Diagnostics:
            [
                new NodeDiagnostic(
                    DiagnosticCodes.UnreadableDocument,
                    DiagnosticSeverity.Error,
                    message,
                    null,
                    exception),
            ]);
}
