using System.Text.Json;
using VisionWeave.Contracts.Workflows;
using VisionWeave.Domain.Workflows;

namespace VisionWeave.Persistence.Workflows;

/// <summary>
/// Writes a workflow document to the versioned <c>.vwflow</c> format. Saving is
/// atomic: the document is written to a temporary file in the destination
/// directory and only then replaces the destination, so a failure leaves the
/// previously saved document untouched.
/// </summary>
public static class WorkflowDocumentWriter
{
    /// <summary>
    /// Writes the document to a stream.
    /// </summary>
    /// <param name="document">The document to write.</param>
    /// <param name="destination">The stream to write to.</param>
    /// <exception cref="NotSupportedException">A parameter holds a value shape the format does not store.</exception>
    public static void Write(WorkflowDocument document, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteNumber(WorkflowJson.SchemaVersion, WorkflowFileFormat.CurrentSchemaVersion);
        writer.WriteString(WorkflowJson.DocumentId, document.Id);
        writer.WriteString(WorkflowJson.Name, document.Name);
        writer.WriteString(WorkflowJson.CreatedUtc, document.CreatedUtc);
        writer.WriteString(WorkflowJson.ModifiedUtc, document.ModifiedUtc);
        writer.WriteNumber(WorkflowJson.Revision, document.Revision);

        if (document.AppVersion is not null)
        {
            writer.WriteString(WorkflowJson.AppVersion, document.AppVersion);
        }

        writer.WriteStartArray(WorkflowJson.RequiredPlugins);
        foreach (string plugin in document.RequiredPlugins.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(plugin);
        }

        writer.WriteEndArray();

        WriteResources(writer, document);

        writer.WriteStartArray(WorkflowJson.Nodes);
        foreach (NodeInstance node in document.Nodes.OrderBy(item => item.InstanceId))
        {
            WriteNode(writer, node);
        }

        writer.WriteEndArray();

        writer.WriteStartArray(WorkflowJson.Connections);
        foreach (WorkflowConnection connection in document.Connections.OrderBy(item => item.ConnectionId))
        {
            WriteConnection(writer, connection);
        }

        writer.WriteEndArray();

        WritePreservedFields(writer, document.ExtensionData);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Saves the document to a path, replacing an existing file only after the
    /// new content has been written in full.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <param name="path">The destination path.</param>
    public static void Save(WorkflowDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        string destination = FullPath(path);
        string directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);

        string temporary = Path.Combine(
            directory,
            $"{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Write(document, stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temporary);
            throw;
        }
    }

    /// <summary>
    /// Saves the recoverable working copy of a document. Autosave writes beside
    /// the document instead of over it, so a failed autosave never damages the
    /// document the user saved deliberately.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <param name="path">The document path the working copy belongs to.</param>
    public static void SaveWorkingCopy(WorkflowDocument document, string path)
        => Save(document, GetWorkingCopyPath(path));

    /// <summary>
    /// Gets the path of the recoverable working copy of a document.
    /// </summary>
    /// <param name="path">The document path.</param>
    /// <returns>The working-copy path.</returns>
    public static string GetWorkingCopyPath(string path)
        => FullPath(path) + WorkflowFileFormat.WorkingCopySuffix;

    private static void WriteNode(Utf8JsonWriter writer, NodeInstance node)
    {
        writer.WriteStartObject();
        writer.WriteString(WorkflowJson.NodeId, node.InstanceId);
        writer.WriteString(WorkflowJson.NodeTypeId, node.NodeTypeId.Value);
        writer.WriteNumber(WorkflowJson.NodeTypeVersion, node.TypeVersion);

        if (node.Label is not null)
        {
            writer.WriteString(WorkflowJson.NodeLabel, node.Label);
        }

        if (!node.IsEnabled)
        {
            writer.WriteBoolean(WorkflowJson.NodeEnabled, false);
        }

        writer.WriteStartObject(WorkflowJson.NodeParameters);
        foreach (KeyValuePair<string, object?> parameter in node.Parameters.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            WorkflowJson.WriteValue(writer, parameter.Key, parameter.Value);
        }

        writer.WriteEndObject();

        WritePortSchemaSnapshot(writer, node);

        writer.WriteStartObject(WorkflowJson.NodeLayout);
        writer.WriteNumber(WorkflowJson.LayoutX, node.Position.X);
        writer.WriteNumber(WorkflowJson.LayoutY, node.Position.Y);
        writer.WriteEndObject();

        writer.WriteStartObject(WorkflowJson.NodeExtensionData);
        WritePreservedFields(writer, node.ExtensionData);
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the remembered ports of a node at the node entry, so a document whose
    /// node definition is unavailable can be rendered from what it recorded. An
    /// empty snapshot is omitted, like every other member that carries its default.
    /// </summary>
    private static void WritePortSchemaSnapshot(Utf8JsonWriter writer, NodeInstance node)
    {
        if (node.PortSchemaSnapshot.Count == 0)
        {
            return;
        }

        writer.WriteStartArray(WorkflowJson.NodePortSchemaSnapshot);

        foreach (PortSchemaEntry port in node.PortSchemaSnapshot)
        {
            writer.WriteStartObject();
            writer.WriteString(WorkflowJson.SnapshotPortId, port.PortId);
            writer.WriteString(WorkflowJson.SnapshotDirection, port.Direction.ToString());

            if (port.TypeId is { } typeId)
            {
                writer.WriteString(WorkflowJson.SnapshotTypeId, typeId.Value);
            }

            if (port.Multiplicity is { } multiplicity)
            {
                writer.WriteString(WorkflowJson.SnapshotMultiplicity, multiplicity.ToString());
            }

            if (port.IsOptional is { } isOptional)
            {
                writer.WriteBoolean(WorkflowJson.SnapshotIsOptional, isOptional);
            }

            if (port.DisplayName is { } displayName)
            {
                writer.WriteString(WorkflowJson.SnapshotDisplayName, displayName);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// Writes the resources the document declares, in the order it records them. An
    /// empty list is omitted, so a document that declares none is written exactly as
    /// it was before the field existed.
    /// </summary>
    private static void WriteResources(Utf8JsonWriter writer, WorkflowDocument document)
    {
        if (document.Resources.Count == 0)
        {
            return;
        }

        writer.WriteStartArray(WorkflowJson.Resources);

        foreach (ResourceReference resource in document.Resources)
        {
            WriteResource(writer, resource);
        }

        writer.WriteEndArray();
    }

    private static void WriteResource(Utf8JsonWriter writer, ResourceReference resource)
    {
        switch (resource)
        {
            case FileResourceReference file:
                writer.WriteStartObject();
                writer.WriteString(WorkflowJson.ResourceKind, WorkflowJson.FileResourceKind);
                writer.WriteString(WorkflowJson.ResourcePath, file.Path);

                if (file.ExpectedSha256 is not null)
                {
                    writer.WriteString(WorkflowJson.ResourceExpectedSha256, file.ExpectedSha256);
                }

                writer.WriteEndObject();
                break;

            case UnknownResourceReference unknown:
                // A kind this build does not model is written back exactly as it was
                // read, so a save never rewrites a reference it cannot interpret.
                writer.WriteRawValue(unknown.Json);
                break;

            default:
                throw new NotSupportedException(
                    $"A '{resource.GetType().Name}' resource is not part of the workflow format.");
        }
    }

    private static void WriteConnection(Utf8JsonWriter writer, WorkflowConnection connection)
    {
        writer.WriteStartObject();
        writer.WriteString(WorkflowJson.ConnectionId, connection.ConnectionId);
        writer.WriteString(WorkflowJson.ConnectionSourceNodeId, connection.SourceNodeId);
        writer.WriteString(WorkflowJson.ConnectionSourcePortId, connection.SourcePortId);
        writer.WriteString(WorkflowJson.ConnectionTargetNodeId, connection.TargetNodeId);
        writer.WriteString(WorkflowJson.ConnectionTargetPortId, connection.TargetPortId);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes preserved fields as they were read, so that a save never silently
    /// drops content this build does not understand.
    /// </summary>
    private static void WritePreservedFields(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> fields)
    {
        foreach (KeyValuePair<string, string> field in fields.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(field.Key);
            writer.WriteRawValue(field.Value);
        }
    }

    private static string FullPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
