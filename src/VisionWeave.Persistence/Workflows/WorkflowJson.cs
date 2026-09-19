using System.Globalization;
using System.Text.Json;

namespace VisionWeave.Persistence.Workflows;

/// <summary>
/// Names the fields of the workflow format and converts the value shapes the
/// format stores. The reader and the writer share it so that a field never
/// travels under two spellings.
/// </summary>
internal static class WorkflowJson
{
    internal const string SchemaVersion = "schemaVersion";
    internal const string DocumentId = "documentId";
    internal const string Name = "name";
    internal const string CreatedUtc = "createdUtc";
    internal const string ModifiedUtc = "modifiedUtc";
    internal const string AppVersion = "appVersion";
    internal const string Revision = "revision";
    internal const string RequiredPlugins = "requiredPlugins";
    internal const string Extensions = "extensions";
    internal const string Nodes = "nodes";
    internal const string Connections = "connections";

    internal const string NodeId = "id";
    internal const string NodeTypeId = "typeId";
    internal const string NodeTypeVersion = "typeVersion";
    internal const string NodeParameters = "parameters";
    internal const string NodeExtensionData = "extensionData";
    internal const string NodeLayout = "layout";
    internal const string NodeLabel = "label";
    internal const string NodeEnabled = "enabled";

    internal const string LayoutX = "x";
    internal const string LayoutY = "y";

    internal const string ConnectionId = "id";
    internal const string ConnectionSourceNodeId = "sourceNodeId";
    internal const string ConnectionSourcePortId = "sourcePortId";
    internal const string ConnectionTargetNodeId = "targetNodeId";
    internal const string ConnectionTargetPortId = "targetPortId";

    /// <summary>
    /// Writes a parameter value. The format stores the JSON value shapes a node
    /// parameter can hold; anything else is a programming error rather than a
    /// value that is silently dropped.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <exception cref="NotSupportedException">The value shape is not part of the format.</exception>
    internal static void WriteValue(Utf8JsonWriter writer, string name, object? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else if (value is bool boolean)
        {
            writer.WriteBoolean(name, boolean);
        }
        else if (value is string text)
        {
            writer.WriteString(name, text);
        }
        else if (value is double number)
        {
            writer.WriteNumber(name, number);
        }
        else if (value is float single)
        {
            writer.WriteNumber(name, single);
        }
        else if (value is decimal precise)
        {
            writer.WriteNumber(name, precise);
        }
        else if (value is long or int or short or sbyte or byte or ushort or uint or ulong)
        {
            writer.WriteNumber(name, Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }
        else
        {
            throw new NotSupportedException(
                $"Parameter '{name}' holds a '{value.GetType().Name}' value, which the workflow format does not store.");
        }
    }

    /// <summary>
    /// Reads a parameter value that the format can store.
    /// </summary>
    /// <param name="element">The stored value.</param>
    /// <param name="value">The value when it can be read.</param>
    /// <returns><see langword="true"/> when the element is a value shape the format stores.</returns>
    internal static bool TryReadValue(JsonElement element, out object? value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                value = null;
                return true;
            case JsonValueKind.True:
                value = true;
                return true;
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.String:
                value = element.GetString();
                return true;
            case JsonValueKind.Number:
                value = element.TryGetInt64(out long integer) ? integer : element.GetDouble();
                return true;
            default:
                value = null;
                return false;
        }
    }
}
