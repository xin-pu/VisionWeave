using Shouldly;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Workflows;
using VisionWeave.Domain.Workflows;
using VisionWeave.Persistence.Tests.Support;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.Persistence.Tests.Workflows;

/// <summary>
/// Covers one editing session over a stored document: what it is bound to, when
/// it is dirty, when it writes, and how a working copy is recovered.
/// </summary>
public sealed class WorkflowSessionTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void New_starts_an_unsaved_clean_document()
    {
        WorkflowSession session = WorkflowSession.New("workflow");

        session.Path.ShouldBeNull();
        session.IsReadOnly.ShouldBeFalse();
        session.IsDirty.ShouldBeFalse();
        session.Diagnostics.ShouldBeEmpty();
        session.Document.Name.ShouldBe("workflow");
        session.Document.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void New_document_is_dirty_after_a_semantic_edit()
    {
        WorkflowSession session = WorkflowSession.New("workflow");

        session.Document.AddNode(SampleDocument.SourceType, 1, new CanvasPosition(0, 0));

        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Saved_document_is_dirty_again_after_a_layout_only_edit()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        NodeInstance node = session.Document.AddNode(SampleDocument.SourceType, 1, new CanvasPosition(0, 0));
        session.Save(_directory.File("moved.vwflow"));
        session.IsDirty.ShouldBeFalse();

        // A move does not move the revision, so the change count is what makes a
        // saved document dirty again.
        session.Document.MoveNode(node.InstanceId, new CanvasPosition(10, 10));

        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Adding_a_resource_marks_the_document_dirty_and_changes_the_revision()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        session.Document.AddNode(SampleDocument.SourceType, 1, new CanvasPosition(0, 0));
        string path = _directory.File("resources.vwflow");
        session.Save(path);
        long revision = session.Document.Revision;

        // A resource is user data a run will read, not a rendering hint, so adding
        // one has to count as an edit that autosave is willing to write.
        session.Document.AddResource(new FileResourceReference("assets/plate.png"));

        session.IsDirty.ShouldBeTrue();
        session.Document.Revision.ShouldBe(revision + 1);

        session.TryAutosave().ShouldBeTrue();

        WorkflowDocument recovered = WorkflowDocumentReader
            .Load(WorkflowDocumentWriter.GetWorkingCopyPath(path))
            .Document!;
        recovered.Resources.ShouldHaveSingleItem()
            .ShouldBe(new FileResourceReference("assets/plate.png"));
    }

    [Fact]
    public void Save_without_a_path_is_refused()
    {
        WorkflowSession session = WorkflowSession.New("workflow");

        Should.Throw<InvalidOperationException>(() => session.Save());
    }

    [Fact]
    public void Save_writes_the_document_and_clears_the_dirty_flag()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        session.Document.AddNode(SampleDocument.SourceType, 1, new CanvasPosition(0, 0));
        string path = _directory.File("saved.vwflow");

        session.Save(path);

        session.Path.ShouldBe(path);
        session.IsDirty.ShouldBeFalse();

        WorkflowLoadResult reloaded = WorkflowDocumentReader.Load(path);
        reloaded.Succeeded.ShouldBeTrue();
        reloaded.Document!.Nodes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Open_restores_the_document_and_the_path_it_came_from()
    {
        string path = _directory.File("open.vwflow");
        WorkflowDocumentWriter.Save(SampleDocument.Build(), path);

        WorkflowSessionResult result = WorkflowSession.Open(path);

        result.Succeeded.ShouldBeTrue();
        WorkflowSession session = result.Session!;
        session.Path.ShouldBe(path);
        session.IsReadOnly.ShouldBeFalse();
        session.IsDirty.ShouldBeFalse();
        session.Document.Name.ShouldBe("Round trip");
        session.Document.Nodes.Count.ShouldBe(2);
        session.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Open_malformed_file_reports_vw_file_001_without_a_session()
    {
        string path = _directory.File("broken.vwflow");
        File.WriteAllText(path, "{ \"documentId\": ");

        WorkflowSessionResult result = WorkflowSession.Open(path);

        result.Succeeded.ShouldBeFalse();
        result.Session.ShouldBeNull();
        result.Diagnostics.ShouldContain(item => item.Code == DiagnosticCodes.UnreadableDocument);
    }

    [Fact]
    public void Open_newer_schema_version_stays_read_only_and_refuses_both_save_paths()
    {
        string path = _directory.File("future.vwflow");
        string copy = _directory.File("copy.vwflow");
        File.WriteAllText(path, Document(schemaVersion: 2));

        WorkflowSessionResult result = WorkflowSession.Open(path);

        WorkflowSession session = result.Session!;
        session.IsReadOnly.ShouldBeTrue();
        session.Diagnostics.ShouldContain(item => item.Code == DiagnosticCodes.UnsupportedDocumentSchema);

        Should.Throw<InvalidOperationException>(() => session.Save());
        Should.Throw<InvalidOperationException>(() => session.Save(copy));
        File.Exists(copy).ShouldBeFalse();
    }

    [Fact]
    public void Autosave_writes_a_working_copy_and_leaves_the_document_dirty()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        string path = _directory.File("autosave.vwflow");
        session.Save(path);
        session.Document.AddNode(SampleDocument.SourceType, 1, new CanvasPosition(0, 0));

        session.TryAutosave().ShouldBeTrue();

        File.Exists(WorkflowDocumentWriter.GetWorkingCopyPath(path)).ShouldBeTrue();
        session.IsDirty.ShouldBeTrue();
        session.Path.ShouldBe(path);
    }

    [Fact]
    public void Autosave_clean_document_writes_nothing()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        string path = _directory.File("clean.vwflow");
        session.Save(path);

        session.TryAutosave().ShouldBeFalse();

        File.Exists(WorkflowDocumentWriter.GetWorkingCopyPath(path)).ShouldBeFalse();
    }

    [Fact]
    public void Autosave_document_without_a_path_writes_nothing()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        session.Document.AddNode(SampleDocument.SourceType, 1, new CanvasPosition(0, 0));

        session.TryAutosave().ShouldBeFalse();
    }

    [Fact]
    public void Recover_restores_the_working_copy_and_stays_bound_to_the_document()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        string path = _directory.File("recover.vwflow");
        session.Document.AddNode(SampleDocument.SourceType, 1, new CanvasPosition(0, 0));
        session.Save(path);
        session.Document.AddNode(SampleDocument.BlurType, 1, new CanvasPosition(200, 0));
        session.TryAutosave().ShouldBeTrue();

        WorkflowSession.HasWorkingCopy(path).ShouldBeTrue();
        WorkflowDocumentReader.Load(path).Document!.Nodes.Count.ShouldBe(1);

        WorkflowSession recovered = WorkflowSession.Recover(path).Session!;

        recovered.Path.ShouldBe(path);
        recovered.Document.Nodes.Count.ShouldBe(2);
        recovered.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Recover_then_save_writes_the_document_and_drops_the_working_copy()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        string path = _directory.File("resolved.vwflow");
        session.Document.AddNode(SampleDocument.SourceType, 1, new CanvasPosition(0, 0));
        session.Save(path);
        session.Document.AddNode(SampleDocument.BlurType, 1, new CanvasPosition(200, 0));
        session.TryAutosave().ShouldBeTrue();

        WorkflowSession recovered = WorkflowSession.Recover(path).Session!;
        recovered.Save();

        recovered.IsDirty.ShouldBeFalse();
        WorkflowSession.HasWorkingCopy(path).ShouldBeFalse();
        WorkflowDocumentReader.Load(path).Document!.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public void HasWorkingCopy_without_an_autosave_reports_nothing_to_recover()
    {
        WorkflowSession session = WorkflowSession.New("workflow");
        string path = _directory.File("never-autosaved.vwflow");
        session.Save(path);

        WorkflowSession.HasWorkingCopy(path).ShouldBeFalse();
    }

    private static string Document(int schemaVersion)
        => $$"""
        {
          "schemaVersion": {{schemaVersion}},
          "documentId": "8d0a6c1e-6a4e-4d5f-9c31-2f0a4a5f6b71",
          "name": "fixture",
          "createdUtc": "2026-09-19T10:00:00+00:00",
          "modifiedUtc": "2026-09-19T11:00:00+00:00",
          "revision": 4,
          "requiredPlugins": [],
          "nodes": [],
          "connections": []
        }
        """;
}
