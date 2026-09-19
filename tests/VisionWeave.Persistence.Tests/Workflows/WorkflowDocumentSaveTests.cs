using Shouldly;
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
