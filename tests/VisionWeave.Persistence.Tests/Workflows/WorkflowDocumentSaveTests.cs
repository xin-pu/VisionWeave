using System.Text.Json;
using Shouldly;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.Persistence.Tests.Support;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.Persistence.Tests.Workflows;

public sealed class WorkflowDocumentSaveTests
{
    [Fact]
    public void Save_second_document_replaces_the_first_and_leaves_no_temporary_file()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("replaced.vwflow");

        WorkflowDocumentWriter.Save(SampleDocument.Build(), path);
        WorkflowDocument replacement = SampleDocument.Build();
        replacement.Rename("Replacement");
        WorkflowDocumentWriter.Save(replacement, path);

        WorkflowDocumentReader.Load(path).Document!.Name.ShouldBe("Replacement");
        Directory.GetFiles(directory.Path).ShouldBe([path]);
    }

    [Fact]
    public void Save_failing_write_keeps_the_previous_document_and_no_temporary_file()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("kept.vwflow");
        WorkflowDocumentWriter.Save(SampleDocument.Build(), path);

        WorkflowDocument unsupported = SampleDocument.Build();
        NodeInstance node = unsupported.Nodes.First();
        unsupported.SetNodeParameter(node.InstanceId, "weird", new Version(1, 0));

        Should.Throw<NotSupportedException>(() => WorkflowDocumentWriter.Save(unsupported, path));

        WorkflowDocumentReader.Load(path).Document!.Name.ShouldBe("Round trip");
        Directory.GetFiles(directory.Path).ShouldBe([path]);
    }

    [Fact]
    public void Save_creates_the_destination_directory()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File(Path.Combine("nested", "created", "workflow.vwflow"));

        WorkflowDocumentWriter.Save(SampleDocument.Build(), path);

        File.Exists(path).ShouldBeTrue();
    }

    [Fact]
    public void Save_emits_resources_and_a_port_schema_snapshot_under_their_own_fields()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("typed-fields.vwflow");

        WorkflowDocumentWriter.Save(SampleDocument.Build(), path);

        using JsonDocument stored = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = stored.RootElement;
        JsonElement resources = root.GetProperty("resources");
        resources.GetArrayLength().ShouldBe(1);
        resources[0].GetProperty("kind").GetString().ShouldBe("file");
        resources[0].GetProperty("path").GetString().ShouldBe("assets/plate.png");

        JsonElement node = root.GetProperty("nodes")
            .EnumerateArray()
            .Single(item => item.GetProperty("typeId").GetString() == "visionweave.opencv.gaussian-blur");

        // The snapshot belongs to the node entry, where the format declares it, and
        // not inside the node's extension data, which holds what this build does not
        // model.
        node.GetProperty("portSchemaSnapshot").GetArrayLength().ShouldBe(2);
        node.GetProperty("extensionData").TryGetProperty("portSchemaSnapshot", out _).ShouldBeFalse();
    }

    [Fact]
    public void Save_omits_a_resource_list_and_a_snapshot_that_are_empty()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("empty-fields.vwflow");
        WorkflowDocument document = WorkflowDocument.Create("empty");
        document.AddNode(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0));

        WorkflowDocumentWriter.Save(document, path);

        string content = File.ReadAllText(path);
        content.ShouldNotContain("\"resources\"");
        content.ShouldNotContain("portSchemaSnapshot");
    }

    [Fact]
    public void Save_working_copy_does_not_change_the_saved_document()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.File("autosaved.vwflow");
        WorkflowDocumentWriter.Save(SampleDocument.Build(), path);

        WorkflowDocument working = SampleDocument.Build();
        working.Rename("Autosaved");
        WorkflowDocumentWriter.SaveWorkingCopy(working, path);

        WorkflowDocumentReader.Load(path).Document!.Name.ShouldBe("Round trip");
        WorkflowDocumentReader.Load(WorkflowDocumentWriter.GetWorkingCopyPath(path))
            .Document!.Name.ShouldBe("Autosaved");
    }
}
