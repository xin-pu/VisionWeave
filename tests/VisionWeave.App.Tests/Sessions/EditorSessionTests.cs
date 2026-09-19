using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.Application.Editing;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.Persistence.Workflows;

namespace VisionWeave.App.Tests.Sessions;

/// <summary>
/// One editing session driven headlessly: no window, no dispatcher, and no STA
/// thread, because the session holds no WPF or Nodify type.
/// </summary>
public sealed class EditorSessionTests : IDisposable
{
    private readonly TemporaryWorkflowDirectory _directory = new();

    [Fact]
    public void NewDocument_starts_untitled_with_no_path_and_no_unsaved_changes()
    {
        EditorSession session = TestSessions.Create();

        session.Document.Name.ShouldBe(EditorSession.UntitledDocumentName);
        session.Path.ShouldBeNull();
        session.IsDirty.ShouldBeFalse();
        session.IsReadOnly.ShouldBeFalse();
        session.Document.Nodes.ShouldBeEmpty();
    }

    [Fact]
    public void Read_leaves_the_document_being_edited_in_place()
    {
        EditorSession session = TestSessions.Create();
        NodeInstance node = session.Document.AddNode(
            new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId),
            1,
            new CanvasPosition(0, 0));
        session.Select([node.InstanceId]);
        WorkflowSession original = session.Session;
        List<string?> raised = [];
        session.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        WorkflowSessionResult read = session.Read(_directory.SaveReadableDocument());

        // Reading produces a document, and saying nothing about it is what lets a
        // shell run the read off the thread its bindings belong to: nothing the
        // window is watching moves until the read is adopted there.
        read.Session.ShouldNotBeNull();
        session.Session.ShouldBeSameAs(original);
        session.Document.Name.ShouldBe(EditorSession.UntitledDocumentName);
        session.Document.Nodes.ShouldHaveSingleItem();
        session.Selection.ShouldBe([node.InstanceId]);
        raised.ShouldBeEmpty();
    }

    [Fact]
    public void Adopt_replaces_the_document_and_clears_the_selection_the_old_one_held()
    {
        EditorSession session = TestSessions.Create();
        NodeInstance node = session.Document.AddNode(
            new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId),
            1,
            new CanvasPosition(0, 0));
        session.Select([node.InstanceId]);
        List<string?> raised = [];
        session.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        WorkflowSessionResult result = session.Adopt(session.Read(_directory.SaveReadableDocument()));

        result.Session.ShouldNotBeNull();
        session.Document.Name.ShouldBe("Saved workflow");
        session.Path.ShouldBe(System.IO.Path.GetFullPath(_directory.PathOf("workflow.vwflow")));

        // A selection names instances of the document it was made in, so adopting a
        // document leaves none of it behind, and the change reaches the shell under
        // every name it reads the document through.
        session.Selection.ShouldBeEmpty();
        raised.ShouldContain(nameof(EditorSession.Session));
        raised.ShouldContain(nameof(EditorSession.Document));
        raised.ShouldContain(nameof(EditorSession.Path));
        raised.ShouldContain(nameof(EditorSession.Projection));
    }

    [Fact]
    public void Adopt_cancelled_after_the_read_keeps_the_current_document()
    {
        EditorSession session = TestSessions.Create();
        WorkflowSession original = session.Session;
        WorkflowSessionResult read = session.Read(_directory.SaveReadableDocument());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Should.Throw<OperationCanceledException>(() => session.Adopt(read, cancellation.Token));

        session.Session.ShouldBeSameAs(original);
    }

    [Fact]
    public void Open_readable_document_adopts_its_path_and_its_name()
    {
        EditorSession session = TestSessions.Create();

        WorkflowSessionResult result = session.Open(_directory.SaveReadableDocument());

        result.Session.ShouldNotBeNull();
        session.Document.Name.ShouldBe("Saved workflow");
        session.Path.ShouldBe(System.IO.Path.GetFullPath(_directory.PathOf("workflow.vwflow")));
        session.IsDirty.ShouldBeFalse();
        session.IsReadOnly.ShouldBeFalse();
        session.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Open_missing_file_reports_vw_file_001_and_keeps_the_current_document()
    {
        EditorSession session = TestSessions.Create();
        WorkflowSession original = session.Session;

        WorkflowSessionResult result = session.Open(_directory.PathOf("absent.vwflow"));

        result.Session.ShouldBeNull();
        session.Session.ShouldBeSameAs(original);
        session.Path.ShouldBeNull();
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
    }

    [Fact]
    public void Open_malformed_file_reports_the_readers_own_diagnostic()
    {
        EditorSession session = TestSessions.Create();

        WorkflowSessionResult result = session.Open(_directory.SaveUnreadableDocument());

        result.Session.ShouldBeNull();
        result.Diagnostics.ShouldContain(item => item.Code == DiagnosticCodes.UnreadableDocument);
        session.Document.Name.ShouldBe(EditorSession.UntitledDocumentName);
    }

    [Fact]
    public void Open_cancelled_before_adoption_keeps_the_current_document()
    {
        EditorSession session = TestSessions.Create();
        WorkflowSession original = session.Session;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Should.Throw<OperationCanceledException>(
            () => session.Open(_directory.SaveReadableDocument(), cancellation.Token));

        session.Session.ShouldBeSameAs(original);
    }

    [Fact]
    public void Added_node_without_a_definition_is_reported_by_the_projection_for_that_node()
    {
        EditorSession session = TestSessions.Create();
        NodeInstance node = session.Document.AddNode(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0));

        session.Execute(new SetNodeParameterCommand(node.InstanceId, "width", 640));

        session.Projection.SeverityOf(node.InstanceId).ShouldBe(DiagnosticSeverity.Error);
        session.IsDocumentValid.ShouldBeFalse();
        session.Projection.Matches(session.Document.Revision).ShouldBeTrue();
    }

    [Fact]
    public void Selection_reports_the_diagnostics_of_the_selected_node_and_of_the_document()
    {
        EditorSession session = TestSessions.Create();
        NodeInstance node = session.Document.AddNode(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0));
        session.Execute(new MoveNodesCommand([(node.InstanceId, new CanvasPosition(10, 10))]));

        session.Select([node.InstanceId]);
        session.SelectionDiagnostics.Diagnostics.ShouldContain(item => item.NodeInstanceId == node.InstanceId);
        session.SelectionDiagnostics.Severity.ShouldBe(DiagnosticSeverity.Error);

        session.Select([Guid.NewGuid()]);
        session.SelectionDiagnostics.Diagnostics.ShouldNotContain(item => item.NodeInstanceId == node.InstanceId);
    }

    [Fact]
    public void Selection_of_a_node_the_document_lost_reports_no_node_diagnostic()
    {
        EditorSession session = TestSessions.Create();
        NodeInstance node = session.Document.AddNode(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0));
        session.Select([node.InstanceId]);

        session.Execute(new RemoveNodeCommand(node.InstanceId));

        session.Selection.ShouldContain(node.InstanceId);
        session.SelectionDiagnostics.Diagnostics.ShouldBeEmpty();
        session.Projection.SeverityOf(node.InstanceId).ShouldBeNull();
    }

    [Fact]
    public void Save_without_a_path_is_refused_with_vw_file_004_and_leaves_the_document_alone()
    {
        EditorSession session = TestSessions.Create();

        NodeDiagnostic refused = session.Save().ShouldHaveSingleItem();

        refused.Code.ShouldBe(DiagnosticCodes.MissingDocumentPath);
        refused.Severity.ShouldBe(DiagnosticSeverity.Error);
        session.Path.ShouldBeNull();
    }

    [Fact]
    public void SaveAs_writes_the_document_and_clears_the_unsaved_changes()
    {
        EditorSession session = TestSessions.Create();
        string path = _directory.PathOf("written.vwflow");

        session.SaveAs(path).ShouldBeEmpty();

        System.IO.File.Exists(path).ShouldBeTrue();
        session.Path.ShouldBe(System.IO.Path.GetFullPath(path));
        session.IsDirty.ShouldBeFalse();
        session.Save().ShouldBeEmpty();
    }

    [Fact]
    public void SaveAs_to_an_unusable_path_reports_vw_file_001_and_leaves_the_document_dirty()
    {
        EditorSession session = TestSessions.Create();
        session.SaveAs(_directory.PathOf("dirty.vwflow")).ShouldBeEmpty();
        session.Document.AddNode(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0));

        // The writer creates a missing folder, so the unusable destination is a path
        // that is already a directory.
        string folder = _directory.PathOf("a-folder");
        System.IO.Directory.CreateDirectory(folder);

        NodeDiagnostic refused = session.SaveAs(folder).ShouldHaveSingleItem();

        refused.Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        refused.Exception.ShouldNotBeNull();
        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Read_only_document_refuses_save_with_vw_file_002()
    {
        EditorSession session = TestSessions.Create();
        session.Open(_directory.SaveUnsupportedSchemaDocument());

        session.IsReadOnly.ShouldBeTrue();
        session.Diagnostics.ShouldContain(item => item.Code == DiagnosticCodes.UnsupportedDocumentSchema);

        NodeDiagnostic refused = session.Save().ShouldHaveSingleItem();

        refused.Code.ShouldBe(DiagnosticCodes.UnsupportedDocumentSchema);
        refused.Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Edit_marks_the_document_dirty_and_save_clears_it()
    {
        EditorSession session = TestSessions.Create();
        string path = _directory.PathOf("edited.vwflow");
        session.SaveAs(path).ShouldBeEmpty();

        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)))
            .IsAccepted.ShouldBeTrue();
        session.IsDirty.ShouldBeTrue();

        session.Save().ShouldBeEmpty();
        session.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Refused_edit_reports_its_diagnostics_and_leaves_the_document_clean()
    {
        EditorSession session = TestSessions.Create();

        DocumentCommandResult result = session.Execute(new SetNodeParameterCommand(Guid.NewGuid(), "width", 640));

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldContain(item => item.Code == DiagnosticCodes.EditRefused);
        session.IsDirty.ShouldBeFalse();
        session.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Undo_and_redo_walk_the_recorded_edits()
    {
        EditorSession session = TestSessions.Create();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));
        session.CanUndo.ShouldBeTrue();
        session.CanRedo.ShouldBeFalse();

        session.Undo().IsAccepted.ShouldBeTrue();
        session.Document.Nodes.ShouldBeEmpty();
        session.CanUndo.ShouldBeFalse();
        session.CanRedo.ShouldBeTrue();

        session.Redo().IsAccepted.ShouldBeTrue();
        session.Document.Nodes.Count.ShouldBe(1);
        session.CanRedo.ShouldBeFalse();
    }

    [Fact]
    public void Undo_without_an_edit_reports_an_empty_history()
    {
        EditorSession session = TestSessions.Create();

        DocumentCommandResult result = session.Undo();

        result.IsAccepted.ShouldBeFalse();
        result.Diagnostics.ShouldContain(item => item.Code == DiagnosticCodes.EmptyHistory);
    }

    [Fact]
    public void NewDocument_discards_the_undo_stack_of_the_document_it_replaces()
    {
        EditorSession session = TestSessions.Create();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        session.New("Second workflow");

        session.CanUndo.ShouldBeFalse();
        session.CanRedo.ShouldBeFalse();
        session.Document.Nodes.ShouldBeEmpty();
        session.Path.ShouldBeNull();
    }

    [Fact]
    public void Autosave_writes_a_working_copy_only_while_the_document_is_dirty()
    {
        EditorSession session = TestSessions.Create();
        string path = _directory.PathOf("autosaved.vwflow");
        session.SaveAs(path).ShouldBeEmpty();

        session.Autosave().Outcome.ShouldBe(AutosaveOutcome.NotApplicable);
        session.HasWorkingCopy(path).ShouldBeFalse();

        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        AutosaveResult written = session.Autosave();

        written.Outcome.ShouldBe(AutosaveOutcome.Written);
        written.Succeeded.ShouldBeTrue();
        written.Diagnostics.ShouldBeEmpty();
        session.HasWorkingCopy(path).ShouldBeTrue();
    }

    [Fact]
    public void Autosave_of_a_document_without_a_path_reports_not_applicable()
    {
        EditorSession session = TestSessions.Create();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        AutosaveResult result = session.Autosave();

        result.Outcome.ShouldBe(AutosaveOutcome.NotApplicable);
        result.Succeeded.ShouldBeFalse();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Autosave_of_a_read_only_document_reports_vw_file_002_without_writing()
    {
        EditorSession session = TestSessions.Create();
        string path = _directory.SaveUnsupportedSchemaDocument("read-only.vwflow");
        session.Open(path).Session.ShouldNotBeNull();
        session.IsReadOnly.ShouldBeTrue();

        AutosaveResult result = session.Autosave();

        result.Outcome.ShouldBe(AutosaveOutcome.Refused);
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnsupportedDocumentSchema);
        session.HasWorkingCopy(path).ShouldBeFalse();
    }

    [Fact]
    public void Autosave_to_an_unusable_path_reports_vw_file_001_and_keeps_the_document_unsaved()
    {
        EditorSession session = TestSessions.Create();
        string path = _directory.PathOf("refused.vwflow");
        session.SaveAs(path).ShouldBeEmpty();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));

        // The working copy is written beside the document, so an unusable
        // destination is a directory sitting where the working copy would go.
        System.IO.Directory.CreateDirectory(WorkflowDocumentWriter.GetWorkingCopyPath(path));

        AutosaveResult result = session.Autosave();

        result.Outcome.ShouldBe(AutosaveOutcome.Failed);
        result.Succeeded.ShouldBeFalse();
        NodeDiagnostic failure = result.Diagnostics.ShouldHaveSingleItem();
        failure.Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        failure.Exception.ShouldNotBeNull();
        session.IsDirty.ShouldBeTrue();
        session.HasWorkingCopy(path).ShouldBeFalse();
    }

    [Fact]
    public void Autosave_of_a_document_with_an_unstorable_value_reports_vw_file_001()
    {
        EditorSession session = TestSessions.Create();
        string path = _directory.PathOf("unstorable.vwflow");
        session.SaveAs(path).ShouldBeEmpty();
        NodeInstance node = session.Document.AddNode(
            new NodeTypeId("visionweave.test.unknown"),
            1,
            new CanvasPosition(0, 0));
        session.Document.SetNodeParameter(node.InstanceId, "unsupported", new Version(1, 0));

        AutosaveResult result = session.Autosave();

        result.Outcome.ShouldBe(AutosaveOutcome.Failed);
        NodeDiagnostic failure = result.Diagnostics.ShouldHaveSingleItem();
        failure.Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        failure.Exception.ShouldBeOfType<NotSupportedException>();
        session.HasWorkingCopy(path).ShouldBeFalse();
    }

    [Fact]
    public async Task Autosave_failure_stays_inside_the_command_boundary()
    {
        EditorSession session = TestSessions.Create();
        string path = _directory.PathOf("boundary.vwflow");
        session.SaveAs(path).ShouldBeEmpty();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));
        System.IO.Directory.CreateDirectory(WorkflowDocumentWriter.GetWorkingCopyPath(path));

        var presenter = new RecordingNotificationPresenter();
        var logger = new RecordingLogger<AsyncCommandBoundary>();
        var boundary = new AsyncCommandBoundary(presenter, logger);

        CommandExecutionResult result = await boundary.RunAsync(
            "Autosave",
            _ => Task.FromResult(session.Autosave().Diagnostics),
            CancellationToken.None);

        // An expected write failure is the operation's own report, so the boundary
        // completes and never classifies it as an unanticipated command failure.
        result.Completion.ShouldBe(CommandCompletion.Completed);
        result.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnreadableDocument);
        logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public void Recover_resumes_the_working_copy_bound_to_the_document_path_with_unsaved_changes()
    {
        EditorSession session = TestSessions.Create();
        string path = _directory.PathOf("recovered.vwflow");
        session.SaveAs(path).ShouldBeEmpty();
        session.Execute(new AddNodeCommand(new NodeTypeId("visionweave.test.unknown"), 1, new CanvasPosition(0, 0)));
        session.Autosave().Succeeded.ShouldBeTrue();

        EditorSession resumed = TestSessions.Create();
        WorkflowSessionResult result = resumed.Recover(path);

        result.Session.ShouldNotBeNull();
        resumed.Path.ShouldBe(System.IO.Path.GetFullPath(path));
        resumed.Document.Nodes.Count.ShouldBe(1);
        resumed.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Recover_without_a_working_copy_reports_a_safe_diagnostic_and_keeps_the_current_document()
    {
        EditorSession session = TestSessions.Create();

        WorkflowSessionResult result = session.Recover(_directory.PathOf("never-autosaved.vwflow"));

        result.Session.ShouldBeNull();
        result.Diagnostics.ShouldContain(item => item.Code == DiagnosticCodes.UnreadableDocument);
        session.Document.Name.ShouldBe(EditorSession.UntitledDocumentName);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
