using System.Globalization;
using Shouldly;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Domain.Workflows;
using VisionWeave.Persistence.Tests.Support;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.Persistence.Tests.Workflows;

public sealed class WorkflowDocumentReaderTests
{
    private static readonly Guid DocumentId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    private static readonly Guid NodeId = Guid.Parse("7b8e0f5a-1c2d-4e3f-8a9b-0c1d2e3f4a5b");

    [Fact]
    public void Load_complete_document_restores_the_stored_metadata()
    {
        using var directory = new TemporaryDirectory();
        string path = Write(directory, Document());
        DateTimeOffset stored = DateTimeOffset.Parse("2026-09-19T10:00:00+00:00", CultureInfo.InvariantCulture);

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeTrue();
        result.IsReadOnly.ShouldBeFalse();
        result.SchemaVersion.ShouldBe(WorkflowFileFormat.CurrentSchemaVersion);
        result.Diagnostics.ShouldBeEmpty();

        WorkflowDocument document = result.Document!;
        document.Id.ShouldBe(DocumentId);
        document.Name.ShouldBe("fixture");
        document.Revision.ShouldBe(4);
        document.CreatedUtc.ShouldBe(stored);
        document.ModifiedUtc.ShouldBe(stored.AddHours(1));
        document.Nodes.Count.ShouldBe(1);
        document.GetNode(NodeId).IsEnabled.ShouldBeTrue();
        document.RequiredPlugins.ShouldBe(["visionweave.opencv"]);
    }

    [Fact]
    public void Load_node_without_a_type_version_treats_it_as_version_zero()
    {
        using var directory = new TemporaryDirectory();
        string nodes = $$"""
        [{ "id": "{{NodeId}}", "typeId": "visionweave.opencv.gaussian-blur", "layout": { "x": 5, "y": 6 } }]
        """;
        string path = Write(directory, Document(nodes: nodes));

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeTrue();
        result.Document!.GetNode(NodeId).TypeVersion.ShouldBe(0);
        result.Document!.GetNode(NodeId).Position.ShouldBe(new CanvasPosition(5, 6));
    }

    [Fact]
    public void Load_unknown_fields_are_preserved_and_re_emitted_by_the_next_save()
    {
        using var directory = new TemporaryDirectory();
        string nodes = $$"""
        [{
          "id": "{{NodeId}}",
          "typeId": "visionweave.opencv.gaussian-blur",
          "typeVersion": 1,
          "parameters": { "kernelSize": 5 },
          "portSchemaSnapshot": [{"portId":"image","direction":"Output"}],
          "extensionData": { "futureNodeField": {"b":[1,2]} },
          "futureLayoutHint": "pinned"
        }]
        """;
        const string extra = """
            "resources": [{"kind":"file","path":"a.png"}], "futureField": {"a":1}
            """;
        string path = Write(directory, Document(nodes: nodes, extra: extra));

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeTrue();
        WorkflowDocument document = result.Document!;
        document.ExtensionData["resources"].ShouldBe("""[{"kind":"file","path":"a.png"}]""");
        document.ExtensionData["futureField"].ShouldBe("""{"a":1}""");

        NodeInstance node = document.GetNode(NodeId);
        node.ExtensionData["portSchemaSnapshot"].ShouldBe("""[{"portId":"image","direction":"Output"}]""");
        node.ExtensionData["futureNodeField"].ShouldBe("""{"b":[1,2]}""");
        node.ExtensionData["futureLayoutHint"].ShouldBe("\"pinned\"");

        string saved = directory.File("re-emitted.vwflow");
        WorkflowDocumentWriter.Save(document, saved);
        string content = File.ReadAllText(saved);

        content.ShouldContain("\"resources\"");
        content.ShouldContain("\"futureField\"");
        content.ShouldContain("portSchemaSnapshot");
        content.ShouldContain("futureLayoutHint");

        WorkflowDocument reloaded = WorkflowDocumentReader.Load(saved).Document!;
        reloaded.ExtensionData["resources"].ShouldBe("""[{"kind":"file","path":"a.png"}]""");
        reloaded.GetNode(NodeId).ExtensionData["portSchemaSnapshot"]
            .ShouldBe("""[{"portId":"image","direction":"Output"}]""");
    }

    [Fact]
    public void Load_newer_schema_version_opens_read_only_with_vw_file_002()
    {
        using var directory = new TemporaryDirectory();
        string path = Write(directory, Document(schemaVersion: 2));

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeTrue();
        result.IsReadOnly.ShouldBeTrue();
        result.SchemaVersion.ShouldBe(2);
        result.Document!.Nodes.Count.ShouldBe(1);
        result.Diagnostics.ShouldContain(item =>
            item.Code == DiagnosticCodes.UnsupportedDocumentSchema
            && item.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Load_malformed_json_reports_vw_file_001_without_a_document()
    {
        using var directory = new TemporaryDirectory();
        string path = Write(directory, "{ \"documentId\": ");

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeFalse();
        result.Document.ShouldBeNull();
        result.Diagnostics.Count.ShouldBe(1);
        result.Diagnostics[0].Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        result.Diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Error);
        result.Diagnostics[0].Exception.ShouldNotBeNull();
    }

    [Fact]
    public void Load_document_without_a_revision_reports_vw_file_001()
    {
        using var directory = new TemporaryDirectory();
        string path = Write(directory, """
        {
          "schemaVersion": 1,
          "documentId": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
          "name": "fixture",
          "createdUtc": "2026-09-19T10:00:00+00:00"
        }
        """);

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeFalse();
        result.Diagnostics[0].Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        result.Diagnostics[0].Message.ShouldContain("revision");
    }

    [Fact]
    public void Load_incoherent_entries_are_skipped_with_vw_file_003()
    {
        using var directory = new TemporaryDirectory();
        string nodes = $$"""
        [
          { "id": "{{NodeId}}", "typeId": "visionweave.opencv.gaussian-blur", "typeVersion": 1 },
          { "id": "{{NodeId}}", "typeId": "visionweave.opencv.resize", "typeVersion": 1 },
          { "id": "not-a-guid", "typeId": "visionweave.opencv.resize", "typeVersion": 1 }
        ]
        """;
        string connections = """
        [{ "id": "2e5c9d1a-6b47-4c8f-9d2e-1a3b4c5d6e7f", "sourceNodeId": "7b8e0f5a-1c2d-4e3f-8a9b-0c1d2e3f4a5b", "sourcePortId": "image", "targetNodeId": "9c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f", "targetPortId": "image" }]
        """;
        string path = Write(directory, Document(nodes: nodes, connections: connections));

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeTrue();
        result.Document!.Nodes.Count.ShouldBe(1);
        result.Document!.Connections.ShouldBeEmpty();
        result.Diagnostics.Count.ShouldBe(3);
        result.Diagnostics.ShouldAllBe(item => item.Code == DiagnosticCodes.DroppedDocumentEntry);
        result.Diagnostics.ShouldContain(item => item.NodeInstanceId == NodeId);
        result.Diagnostics.ShouldContain(item => item.Message.Contains("no identifier", StringComparison.Ordinal));
        result.Diagnostics.ShouldContain(item => item.Message.Contains("does not contain", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_parameter_the_format_does_not_store_is_skipped_with_vw_file_003()
    {
        using var directory = new TemporaryDirectory();
        string nodes = $$"""
        [{
          "id": "{{NodeId}}",
          "typeId": "visionweave.opencv.gaussian-blur",
          "typeVersion": 1,
          "parameters": { "kernelSize": 5, "nested": { "a": 1 } }
        }]
        """;
        string path = Write(directory, Document(nodes: nodes));

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeTrue();
        NodeInstance node = result.Document!.GetNode(NodeId);
        node.Parameters.Keys.ShouldBe(["kernelSize"]);
        result.Diagnostics.Count.ShouldBe(1);
        result.Diagnostics[0].Code.ShouldBe(DiagnosticCodes.DroppedDocumentEntry);
        result.Diagnostics[0].NodeInstanceId.ShouldBe(NodeId);
        result.Diagnostics[0].Message.ShouldContain("nested");
    }

    [Fact]
    public void Load_root_that_is_not_an_object_reports_vw_file_001()
    {
        using var directory = new TemporaryDirectory();
        string path = Write(directory, "[1, 2, 3]");

        WorkflowLoadResult result = WorkflowDocumentReader.Load(path);

        result.Succeeded.ShouldBeFalse();
        result.Diagnostics[0].Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
    }

    private static string Write(TemporaryDirectory directory, string content)
    {
        string path = directory.File($"{Guid.NewGuid():N}.vwflow");
        File.WriteAllText(path, content);
        return path;
    }

    private static string Document(
        string? nodes = null,
        string connections = "[]",
        int schemaVersion = 1,
        string? extra = null)
    {
        nodes ??= $$"""
        [{ "id": "{{NodeId}}", "typeId": "visionweave.opencv.gaussian-blur", "typeVersion": 1, "parameters": { "kernelSize": 5 }, "layout": { "x": 1, "y": 2 } }]
        """;

        string optional = extra is null ? string.Empty : $",{Environment.NewLine}{extra}";

        return $$"""
        {
          "schemaVersion": {{schemaVersion}},
          "documentId": "{{DocumentId}}",
          "name": "fixture",
          "createdUtc": "2026-09-19T10:00:00+00:00",
          "modifiedUtc": "2026-09-19T11:00:00+00:00",
          "revision": 4,
          "requiredPlugins": ["visionweave.opencv"],
          "nodes": {{nodes}},
          "connections": {{connections}}{{optional}}
        }
        """;
    }
}
