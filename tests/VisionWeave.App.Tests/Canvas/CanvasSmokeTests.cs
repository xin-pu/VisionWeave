using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Nodify;
using OpenCvSharp;
using Shouldly;
using VisionWeave.App.Canvas;
using VisionWeave.App.Commands;
using VisionWeave.App.Composition;
using VisionWeave.App.Inspector;
using VisionWeave.App.Notifications;
using VisionWeave.App.Preview;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Execution;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;
using VisionWeave.OpenCv.Preview;

// OpenCvSharp declares a window of its own, and this file names one to read the image
// the run wrote, so the window the shell draws is reached through an alias.
using Window = System.Windows.Window;

namespace VisionWeave.App.Tests.Canvas;

/// <summary>
/// Drives the real window: the application's theme and templates, the markup's
/// bindings, and Nodify's containers and connectors, rather than the view models
/// they are bound to. One test walks every flow the shell promises — placing,
/// connecting, selecting, deleting, and rewinding a workflow; editing a parameter,
/// reading the condition it earned, saving, and opening the file again; running a
/// stored workflow over a real image and seeing what it produced — because the
/// window is built here under one <see cref="System.Windows.Application"/> for the
/// process, which is what WPF allows. Each step asserts on the elements a user would
/// be looking at.
/// </summary>
public sealed class CanvasSmokeTests
{
    [Fact]
    public void The_shell_places_connects_selects_deletes_and_rewinds_a_workflow_on_the_canvas()
        => StaThread.Run(() =>
        {
            // Work that runs off the window's thread is posted back to the thread that
            // owns it, so the test installs the context a running application has and
            // lets that thread run what was posted to it.
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            // The window is built from the application's own resources: the framework
            // theme, the tokens, and the shell styles are the ones that ship.
            var application = new App();
            application.InitializeComponent();

            NodeDefinitionCatalog catalog = NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);
            EditorSession session = TestSessions.Create(catalog: catalog);
            ShellStatus status = new(session);
            RecordingSnackbarService snackbar = new();
            StubFileChooser chooser = new();
            var boundaryLog = new RecordingLogger<AsyncCommandBoundary>();
            var boundary = new AsyncCommandBoundary(new RecordingNotificationPresenter(), boundaryLog);
            var openDocument = new OpenDocumentCommand(boundary, session, status);

            // The preview is drawn through the presentation the application composes:
            // frames are converted where the run executes and shown on this thread. The
            // executors are the ones the shell resolves, with the resize node turned
            // into one that waits, so the flow can stop a run while a node is running.
            var preview = new PreviewViewModel(session);
            var ledger = new LeaseLedger();
            var executors = new RunHold(new ExecutorRegistry(ledger));
            var runWorkflow = new RunWorkflowCommand(
                boundary,
                session,
                new WorkflowSnapshotFactory(catalog),
                new ExecutionPlanBuilder(),
                new WorkflowRunner(
                    executors,
                    ledger,
                    ExecutionOptions.Default,
                    new RunPreviewObserver(
                        FramePreviewConverter.Default,
                        catalog,
                        new ShellRunPreviewPresenter(preview, Dispatcher.CurrentDispatcher))),
                status);

            var viewModel = new MainWindowViewModel(
                session,
                catalog,
                new WorkflowValidator(catalog),
                openDocument,
                new SaveDocumentCommand(boundary, session, status, chooser),
                runWorkflow,
                preview,
                chooser,
                new ShellPromptViewModel(),
                status);
            var window = new MainWindow(viewModel, new SnackbarNotificationPresenter(snackbar));
            CanvasViewModel canvas = viewModel.Canvas;
            Shell shell = new(window, viewModel, session, canvas, status, snackbar, chooser, preview, ledger);

            using TemporaryWorkflowDirectory directory = new();

            window.Show();
            Lay(window);

            PlaceConnectSelectDeleteAndRewind(shell);
            EditDiagnoseSaveAndReopen(shell, directory, boundaryLog);
            BlockEditsToAnUnsupportedDocument(shell, directory);
            RunTheWorkflowAndShowItsPreview(shell, directory, executors);

            window.Close();
        });

    /// <summary>
    /// The window and the objects it was built with, so each flow drives the shell a
    /// user drives rather than reassembling one of its parts.
    /// </summary>
    /// <param name="Window">The window the flows act on.</param>
    /// <param name="ViewModel">The shell's view model.</param>
    /// <param name="Session">The editing session the document belongs to.</param>
    /// <param name="Canvas">The work surface.</param>
    /// <param name="Status">The durable state the status area reports.</param>
    /// <param name="Snackbar">What the shell announced while a flow ran.</param>
    /// <param name="Chooser">The file dialog, answered by a flow instead of by a user.</param>
    /// <param name="Preview">The managed preview the run publishes through.</param>
    /// <param name="Ledger">The ledger every frame the shell's run creates is reported to.</param>
    private sealed record Shell(
        Window Window,
        MainWindowViewModel ViewModel,
        EditorSession Session,
        CanvasViewModel Canvas,
        ShellStatus Status,
        RecordingSnackbarService Snackbar,
        StubFileChooser Chooser,
        PreviewViewModel Preview,
        LeaseLedger Ledger);

    /// <summary>
    /// Places, connects, selects, deletes, and rewinds a workflow, and reads what the
    /// document says about a connection it refused.
    /// </summary>
    /// <param name="shell">The shell being driven.</param>
    private static void PlaceConnectSelectDeleteAndRewind(Shell shell)
    {
        Window window = shell.Window;
        CanvasViewModel canvas = shell.Canvas;
        EditorSession session = shell.Session;
        ShellStatus status = shell.Status;
        RecordingSnackbarService snackbar = shell.Snackbar;
        MainWindowViewModel viewModel = shell.ViewModel;

        // The editor reports where it is looking, which is what lets a node land in
        // the middle of the surface instead of somewhere off it.
        canvas.ViewportSize.Width.ShouldBeGreaterThan(0);
        canvas.ViewportSize.Height.ShouldBeGreaterThan(0);
        canvas.Nodes.ShouldBeEmpty();

        // The tag row narrows the catalogue the user picks from: the chip carries the
        // command the view model offers, and choosing it leaves the types that carry
        // the word, which is what makes a tag an axis rather than a label.
        Button smoothing = Button(window, OpenCvNodeTags.Smoothing);
        smoothing.Command.ShouldBeSameAs(viewModel.ToggleTagCommand);
        smoothing.Command.Execute(smoothing.CommandParameter);
        Lay(window);

        // Rebuilding the row replaces every chip, so the selected one is read again
        // here rather than kept from the step before.
        Button inForce = Button(window, OpenCvNodeTags.Smoothing);
        inForce.DataContext.ShouldBeOfType<ShellCatalogueTag>().IsSelected.ShouldBeTrue();
        viewModel.CatalogueGroups.SelectMany(group => group.Entries).Select(entry => entry.DisplayName)
            .ShouldBe(["Bilateral Filter", "Blur", "Gaussian Blur", "Median Blur"]);
        Descendants<Button>(window).ShouldNotContain(
            button => (string?)button.CommandParameter == OpenCvNodeIds.DrawCircleTypeId);

        // The first chip clears the filter, so the row offers the whole catalogue
        // again rather than leaving a second filter in force.
        Button all = Button(window, "All");
        all.Command.Execute(all.CommandParameter);
        Lay(window);

        viewModel.CatalogueGroups.Sum(group => group.Entries.Count).ShouldBe(25);

        // The document may be written back, so neither surface it is edited on says
        // otherwise. This is the reading the later flows are compared against.
        Notice(shell, "CanvasRegion").Visibility.ShouldBe(Visibility.Collapsed);
        Notice(shell, "InspectorRegion").Visibility.ShouldBe(Visibility.Collapsed);

        // Placing a node: the catalogue entry the user clicks carries the canvas's
        // own command, and the surface draws what the document now holds.
        Button entry = Button(window, OpenCvNodeIds.GaussianBlurTypeId);
        entry.Command.ShouldBeSameAs(canvas.AddNodeCommand);
        entry.Command.Execute(entry.CommandParameter);
        Lay(window);

        ItemContainer blurContainer = Containers(window).ShouldHaveSingleItem();
        WorkflowNodeViewModel blur = canvas.Nodes.ShouldHaveSingleItem();
        blurContainer.DataContext.ShouldBeSameAs(blur);
        Descendants<TextBlock>(blurContainer).ShouldContain(block => block.Text == "Gaussian Blur");

        // The ports are drawn, and the connector control published the point a
        // wire attaches to for each of them.
        Connector[] blurPorts = [.. Descendants<Connector>(blurContainer)];
        blurPorts.Length.ShouldBe(2);
        blurPorts.ShouldAllBe(port => port.Anchor != default);
        foreach (Connector port in blurPorts)
        {
            Ellipse indicator = Descendants<Ellipse>(port).Single(ellipse => ellipse.Stroke is not null);
            indicator.Fill.ShouldNotBeNull();
            indicator.Stroke.ShouldNotBeNull();
            port.Template.FindName("PART_Connector", port).ShouldBeOfType<Ellipse>();
        }

        canvas.AddNodeCommand.Execute(OpenCvNodeIds.ResizeTypeId);
        Lay(window);

        Containers(window).Count.ShouldBe(2);

        // Adding redraws the whole surface, so the presentations are read again
        // here rather than kept from the step before.
        blur = canvas.Nodes.Single(node => node.DisplayName == "Gaussian Blur");
        WorkflowNodeViewModel resize = canvas.Nodes.Single(node => node.DisplayName == "Resize");

        // The wire a drag draws follows the pointer: it starts where the port the
        // drag began at published, and it is drawn from that end, so a drag that
        // began at an input is drawn backwards to keep the curve beside the pointer.
        PortViewModel draggedInput = blur.Inputs.ShouldHaveSingleItem();
        Connector started = Port(window, draggedInput);

        started.BeginConnecting();
        started.UpdatePendingConnection(new System.Windows.Point(started.Anchor.X - 120, started.Anchor.Y));
        Lay(window);

        PendingConnection pending = Descendants<PendingConnection>(window).ShouldHaveSingleItem();
        pending.IsVisible.ShouldBeTrue();
        pending.Source.ShouldBeSameAs(draggedInput);
        pending.Direction.ShouldBe(ConnectionDirection.Backward);

        // A drag the user lets go of over nothing is not an edit: the wire it drew
        // goes away and the document is never asked about it.
        started.CancelConnecting();
        Lay(window);

        pending.IsVisible.ShouldBeFalse();
        canvas.Connectors.ShouldBeEmpty();
        status.Condition.ShouldBe(ShellStatus.NoConditionText);

        // Connecting: a valid wire is drawn between the two points the ports
        // published, so it follows the ports rather than a copy of where they were.
        Drag(window, blur.Outputs.ShouldHaveSingleItem(), resize.Inputs.ShouldHaveSingleItem());
        Lay(window);

        WorkflowConnectionViewModel wire = canvas.Connectors.ShouldHaveSingleItem();
        LineConnection drawn = Wires(window, canvas).ShouldHaveSingleItem();
        drawn.Source.ShouldBe(wire.Source.Anchor);
        drawn.Target.ShouldBe(wire.Target.Anchor);
        wire.Source.Anchor.ShouldNotBe(default);
        wire.Target.Anchor.ShouldNotBe(default);

        // Selecting: the container writes the flag while the user selects, and the
        // shell reports the one selection it holds — both nodes, once the second
        // is added to the selection the way a click on it would.
        ItemContainer selected = Containers(window)
            .Single(container => ((WorkflowNodeViewModel)container.DataContext).InstanceId == blur.InstanceId);
        selected.IsSelected = true;
        Containers(window)
            .Single(container => ((WorkflowNodeViewModel)container.DataContext).InstanceId == resize.InstanceId)
            .IsSelected = true;

        session.Selection.ShouldBe([blur.InstanceId, resize.InstanceId], ignoreOrder: true);
        canvas.DeleteSelectionCommand.CanExecute(null).ShouldBeTrue();

        // Deleting: the header button carries the canvas's command, and one
        // gesture takes the node, its wire, and the containers that drew them.
        Button delete = Button(window, "_Delete");
        delete.Command.ShouldBeSameAs(canvas.DeleteSelectionCommand);
        delete.Command.Execute(null);
        Lay(window);

        Containers(window).ShouldBeEmpty();
        Wires(window, canvas).ShouldBeEmpty();
        canvas.IsEmpty.ShouldBeTrue();

        // Undo brings the whole gesture back, wire included; redo takes it away.
        canvas.UndoCommand.Execute(null);
        Lay(window);

        Containers(window).Count.ShouldBe(2);
        Wires(window, canvas).ShouldHaveSingleItem();

        canvas.RedoCommand.Execute(null);
        Lay(window);

        Containers(window).ShouldBeEmpty();

        canvas.UndoCommand.Execute(null);
        Lay(window);

        Containers(window).Count.ShouldBe(2);

        // A refused connection: two inputs cannot be joined. The drag drew a
        // pending wire while it was in progress, and the document turned it down,
        // so the surface is the very projection the drag started from — the same
        // presentations, the same wires, and nothing to roll back.
        IReadOnlyList<WorkflowNodeViewModel> nodesBefore = canvas.Nodes;
        IReadOnlyList<WorkflowConnectionViewModel> wiresBefore = canvas.Connectors;
        blur = canvas.Nodes.Single(node => node.DisplayName == "Gaussian Blur");
        resize = canvas.Nodes.Single(node => node.DisplayName == "Resize");

        Drag(window, blur.Inputs.ShouldHaveSingleItem(), resize.Inputs.ShouldHaveSingleItem());
        Lay(window);

        canvas.Nodes.ShouldBeSameAs(nodesBefore);
        canvas.Connectors.ShouldBeSameAs(wiresBefore);
        Wires(window, canvas).ShouldHaveSingleItem();
        Containers(window).Count.ShouldBe(2);

        // The refusal is not an edit, so the step the user could redo is still
        // there: an accepted edit is what clears the redo stack.
        session.CanRedo.ShouldBeTrue();

        // The refusal is reported where it stays readable: the status area names
        // the code, and the canvas does not interrupt the user with an announcement.
        status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        status.Condition.ShouldContain(DiagnosticCodes.IncompatiblePort);
        snackbar.Announcements.ShouldBeEmpty();
    }

    /// <summary>
    /// Edits a parameter through the field a user types in, reads the condition the
    /// value earned at the parameter it belongs to, writes the document, and opens the
    /// file it was written to.
    /// </summary>
    /// <param name="shell">The shell being driven.</param>
    /// <param name="directory">The directory the document is written into.</param>
    /// <param name="log">What the command boundary logged, which the flow reads to prove a step did not fail behind the window.</param>
    private static void EditDiagnoseSaveAndReopen(
        Shell shell,
        TemporaryWorkflowDirectory directory,
        RecordingLogger<AsyncCommandBoundary> log)
    {
        // Selecting a node the way a click selects it is what makes its parameters
        // the inspector's subject.
        Select(shell, "Gaussian Blur");

        InspectorViewModel inspector = shell.ViewModel.Inspector;
        inspector.IsEditable.ShouldBeTrue();
        inspector.NodeTitle.ShouldBe("Gaussian Blur");

        // The field the window drew for the parameter this flow edits, and the gesture
        // the window bound to it: what a user does here is type and press Enter.
        ParameterEditorViewModel kernel = Parameter(shell, OpenCvNodeIds.KernelSizeParameter);
        TextBox field = Field(shell, kernel);
        field.Text.ShouldBe("5");

        KeyBinding enter = Gesture(field);
        enter.Command.ShouldBeSameAs(kernel.ApplyCommand);

        // A value the definition cannot read as a number is refused in the field and
        // never reaches the document, so the document keeps the revision it had.
        long revision = shell.Session.Document.Revision;
        field.Text = "not a number";
        enter.Command.Execute(null);
        Lay(shell.Window);

        shell.Session.Document.Revision.ShouldBe(revision);
        shell.Session.Document.GetNode(NodeOf(shell)).Parameters
            .ShouldNotContainKey(OpenCvNodeIds.KernelSizeParameter);

        // The refusal is shown on the field it belongs to and reported where a
        // condition stays readable, rather than being dropped on the floor.
        kernel = Parameter(shell, OpenCvNodeIds.KernelSizeParameter);
        kernel.Condition.ShouldContain(DiagnosticCodes.InvalidParameterValue);
        kernel.Severity.ShouldBe(DiagnosticSeverity.Error);
        shell.Status.Condition.ShouldContain(DiagnosticCodes.InvalidParameterValue);

        // A value the definition refuses is committed and reported at the parameter it
        // belongs to, so the panel and the field point at kernelSize rather than at the
        // node, and the mark is drawn under the field the value was typed in.
        field.Text = "4096";
        enter.Command.Execute(null);
        Lay(shell.Window);

        KernelSize(shell).ShouldBe(4096L);
        shell.Status.Condition.ShouldContain(DiagnosticCodes.ParameterOutOfRange);
        shell.ViewModel.Inspector.Diagnostics
            .ShouldContain(entry =>
                entry.Code == DiagnosticCodes.ParameterOutOfRange
                && entry.Target == $"parameter {OpenCvNodeIds.KernelSizeParameter}"
                && entry.SeverityWord == "Error");

        ParameterEditorViewModel marked = Parameter(shell, OpenCvNodeIds.KernelSizeParameter);
        Descendants<TextBlock>(shell.Window).ShouldContain(block =>
            ReferenceEquals(block.DataContext, marked) && block.Text == marked.Condition);

        // A value the definition accepts clears the condition the last one earned.
        Descendants<TextBox>(shell.Window).ShouldContain(field);
        field.Text.ShouldBe("4096");
        field.Text = "12";
        Parameter(shell, OpenCvNodeIds.KernelSizeParameter).Text.ShouldBe("12");
        enter.Command.Execute(null);
        Lay(shell.Window);

        Parameter(shell, OpenCvNodeIds.KernelSizeParameter).Condition.ShouldBeEmpty();
        KernelSize(shell).ShouldBe(12L);
        shell.ViewModel.Inspector.Diagnostics
            .ShouldNotContain(entry => entry.Code == DiagnosticCodes.ParameterOutOfRange);
        shell.Status.Condition.ShouldBe(ShellStatus.NoConditionText);

        // Two commits of one parameter are one undo step, because a parameter is the
        // unit a user thinks in and a keystroke is not: one undo takes the field back
        // to what it held before the first of them, and one redo brings it back.
        shell.Canvas.UndoCommand.Execute(null);
        Lay(shell.Window);

        Parameter(shell, OpenCvNodeIds.KernelSizeParameter).HasStoredValue.ShouldBeFalse();
        Field(shell, Parameter(shell, OpenCvNodeIds.KernelSizeParameter)).Text.ShouldBe("5");
        shell.Session.Document.GetNode(NodeOf(shell)).Parameters
            .ShouldNotContainKey(OpenCvNodeIds.KernelSizeParameter);

        // The step belongs to the parameter and to nothing else: the wire the earlier
        // step connected is still drawn.
        shell.Canvas.Connectors.ShouldHaveSingleItem();

        shell.Canvas.RedoCommand.Execute(null);
        Lay(shell.Window);

        KernelSize(shell).ShouldBe(12L);

        // Saving writes the document where the shell asks for it, because this one has
        // never been saved, and the file is what carries the edit from here on.
        string path = directory.PathOf("sketch.vwflow");
        shell.Chooser.AnswerSave(path);

        Button save = Button(shell.Window, "_Save");
        save.Command.ShouldBeSameAs(shell.ViewModel.SaveCommand);
        save.Command.Execute(null);
        Lay(shell.Window);

        System.IO.File.Exists(path).ShouldBeTrue();
        System.IO.File.ReadAllText(path).ShouldContain($"\"{OpenCvNodeIds.KernelSizeParameter}\": 12");
        shell.Session.Path.ShouldBe(path);
        shell.Session.IsDirty.ShouldBeFalse();
        shell.Status.DocumentState.ShouldBe("Saved");

        // A later edit leaves the file behind, so opening it asks what to do with the
        // changes the document still holds instead of replacing them.
        field.Text = "21";
        enter.Command.Execute(null);
        Lay(shell.Window);

        KernelSize(shell).ShouldBe(21L);
        shell.Session.IsDirty.ShouldBeTrue();

        shell.Chooser.Answer(path);
        shell.ViewModel.OpenCommand.Execute(null);
        Settle(shell, () => shell.ViewModel.Prompt.IsOpen);
        Lay(shell.Window);

        // The question names the file it belongs to, and its three answers are the ones
        // the flow offered rather than words the markup chose.
        shell.ViewModel.Prompt.Question.ShouldContain("sketch.vwflow");
        shell.ViewModel.Prompt.Question.ShouldContain("unsaved changes");
        shell.ViewModel.Prompt.AcceptText.ShouldBe("Save");
        shell.ViewModel.Prompt.RefuseText.ShouldBe("Discard");
        shell.ViewModel.Prompt.CancelText.ShouldBe("Cancel");

        // Discarding is what lets the flow continue, and what the file holds is then
        // what is open.
        shell.ViewModel.Prompt.RefuseCommand.Execute(null);
        Settle(shell, () => !shell.Session.IsDirty);
        Lay(shell.Window);

        // The document was replaced whole: the selection the discarded document held
        // went with it, and the shell says what the file holds and nothing about a
        // failure. A read that ran away from the thread the window belongs to would
        // leave the selection behind, because the notification that clears it is the
        // one a binding refuses to accept.
        shell.ViewModel.Prompt.IsOpen.ShouldBeFalse();
        shell.Session.Path.ShouldBe(path);
        shell.ViewModel.DocumentTitle.ShouldBe("sketch.vwflow");
        shell.Session.Selection.ShouldBeEmpty();
        shell.Status.Condition.ShouldBe(ShellStatus.NoConditionText);
        log.Entries.ShouldBeEmpty("the shell must not have to catch a failure the flow did not report.");
        shell.Canvas.Nodes.Count.ShouldBe(2);
        shell.Canvas.Connectors.ShouldHaveSingleItem();

        // What was discarded is gone and what was written is on screen: the field is
        // drawn again with the value the file holds rather than the one that was typed.
        Select(shell, "Gaussian Blur");

        ParameterEditorViewModel reopened = Parameter(shell, OpenCvNodeIds.KernelSizeParameter);
        reopened.HasStoredValue.ShouldBeTrue();
        reopened.Text.ShouldBe("12");
        Field(shell, reopened).Text.ShouldBe("12");
        KernelSize(shell).ShouldBe(12L);
    }

    /// <summary>
    /// Opens a document this build must not write back and checks that the shell shows
    /// it without offering an edit: the reason is drawn on both surfaces the document
    /// would be edited on, every editing gesture is disabled, and one that reaches the
    /// canvas or the session anyway changes nothing.
    /// </summary>
    /// <param name="shell">The shell being driven.</param>
    /// <param name="directory">The directory the document is written into.</param>
    private static void BlockEditsToAnUnsupportedDocument(Shell shell, TemporaryWorkflowDirectory directory)
    {
        // A document this build reads and must not write, holding one node so the shell
        // has something to show and something to refuse to edit.
        WorkflowDocument stored = WorkflowDocument.Create("A newer workflow");
        stored.AddNode(new NodeTypeId(OpenCvNodeIds.GaussianBlurTypeId), 1, new CanvasPosition(0, 0));

        shell.Chooser.Answer(directory.SaveUnsupportedSchemaDocument(stored, "newer.vwflow"));
        shell.ViewModel.OpenCommand.Execute(null);

        // The document was written back a moment ago, so opening the file asks nothing.
        shell.ViewModel.Prompt.IsOpen.ShouldBeFalse();

        Settle(shell, () => shell.Session.IsReadOnly);
        Lay(shell.Window);

        // The shell says why, and says it where the document would be edited rather
        // than as a condition of a node that is perfectly fine.
        shell.ViewModel.IsReadOnly.ShouldBeTrue();
        shell.ViewModel.ReadOnlyNote.ShouldContain("opened read-only");
        Notice(shell, "CanvasRegion").Visibility.ShouldBe(Visibility.Visible);
        Notice(shell, "InspectorRegion").Visibility.ShouldBe(Visibility.Visible);
        shell.Session.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.Code == DiagnosticCodes.UnsupportedDocumentSchema);

        // A control the user cannot use is disabled rather than answering with a
        // refusal: the catalogue offers no addable type, and the header's delete is
        // disabled even with a node selected.
        Button(shell.Window, OpenCvNodeIds.ResizeTypeId).IsEnabled.ShouldBeFalse();

        Containers(shell.Window).ShouldHaveSingleItem().IsSelected = true;
        Lay(shell.Window);

        shell.Session.Selection.ShouldHaveSingleItem();
        Button(shell.Window, "_Delete").IsEnabled.ShouldBeFalse();

        // The fields are still drawn, so the shell can say what the node holds, but the
        // whole list is disabled and the inspector says it is not editable.
        InspectorViewModel inspector = shell.ViewModel.Inspector;
        inspector.Parameters.ShouldNotBeEmpty();
        inspector.IsEditable.ShouldBeFalse();
        List(shell.Window, inspector.Parameters).IsEnabled.ShouldBeFalse();

        // The rule belongs to the session rather than to the markup, so a gesture that
        // reaches the canvas, or the session directly, leaves the document as it was.
        long revision = shell.Session.Document.Revision;

        shell.Canvas.AddNodeCommand.Execute(OpenCvNodeIds.ResizeTypeId);
        shell.Canvas.DeleteSelectionCommand.Execute(null);
        shell.Session
            .Execute(new AddNodeCommand(
                new NodeTypeId(OpenCvNodeIds.ResizeTypeId),
                1,
                new CanvasPosition(0, 0)))
            .IsAccepted.ShouldBeFalse();
        Lay(shell.Window);

        shell.Session.Document.Revision.ShouldBe(revision);
        shell.Session.Document.Nodes.ShouldHaveSingleItem();
        shell.Canvas.Nodes.ShouldHaveSingleItem();
        shell.Canvas.Connectors.ShouldBeEmpty();
    }

    /// <summary>
    /// Runs a workflow over a real image — placed and wired by the gestures the canvas
    /// offers, its document written beside that image, the run started from the header —
    /// and then stops a run while a node is executing.
    /// </summary>
    private static void RunTheWorkflowAndShowItsPreview(
        Shell shell,
        TemporaryWorkflowDirectory directory,
        RunHold executors)
    {
        Window window = shell.Window;
        CanvasViewModel canvas = shell.Canvas;
        EditorSession session = shell.Session;

        // The image sits beside the document, because that is the folder the run
        // resolves the workflow's file names against.
        using var plate = new Mat(24, 32, MatType.CV_8UC3, Scalar.All(120));
        Cv2.ImWrite(directory.PathOf("plate.png"), plate).ShouldBeTrue();

        session.New("A workflow to run");
        Lay(window);

        shell.ViewModel.IsReadOnly.ShouldBeFalse();
        Notice(shell, "CanvasRegion").Visibility.ShouldBe(Visibility.Collapsed);
        canvas.IsEmpty.ShouldBeTrue();

        // Nothing has run in this document, so the region says what it is waiting for.
        TextBlock waiting = Descendants<TextBlock>(Named<Border>(window, "PreviewRegion"))
            .Single(block => block.Text == PreviewViewModel.NoPreviewText);

        waiting.Text.ShouldBe(PreviewViewModel.NoPreviewText);
        waiting.Visibility.ShouldBe(Visibility.Visible);

        Button entry = Button(window, OpenCvNodeIds.ImageSourceTypeId);
        entry.Command.ShouldBeSameAs(canvas.AddNodeCommand);
        entry.Command.Execute(entry.CommandParameter);
        Lay(window);

        Button(window, OpenCvNodeIds.ResizeTypeId).Command.Execute(OpenCvNodeIds.ResizeTypeId);
        Lay(window);

        Button(window, OpenCvNodeIds.SaveImageTypeId).Command.Execute(OpenCvNodeIds.SaveImageTypeId);
        Lay(window);

        WorkflowNodeViewModel image = canvas.Nodes.Single(node => node.DisplayName == "Image Source");
        WorkflowNodeViewModel resize = canvas.Nodes.Single(node => node.DisplayName == "Resize");
        WorkflowNodeViewModel save = canvas.Nodes.Single(node => node.DisplayName == "Save Image");

        Drag(window, image.Outputs.ShouldHaveSingleItem(), resize.Inputs.ShouldHaveSingleItem());
        Lay(window);

        // Connecting redraws the whole surface, so the second drag leaves from the
        // nodes and connectors drawn by the redraw rather than from the ones the step
        // before held.
        resize = canvas.Nodes.Single(node => node.DisplayName == "Resize");
        save = canvas.Nodes.Single(node => node.DisplayName == "Save Image");
        Drag(window, resize.Outputs.ShouldHaveSingleItem(), save.Inputs.ShouldHaveSingleItem());
        Lay(window);

        canvas.Connectors.Count.ShouldBe(2);
        Wires(window, canvas).Count.ShouldBe(2);

        SetParameter(shell, image.InstanceId, OpenCvNodeIds.PathParameter, "plate.png");
        SetParameter(shell, resize.InstanceId, OpenCvNodeIds.WidthParameter, 16);
        SetParameter(shell, resize.InstanceId, OpenCvNodeIds.HeightParameter, 8);
        SetParameter(shell, save.InstanceId, OpenCvNodeIds.PathParameter, "done.png");

        // The document has to be on disk before it runs, because its own folder is what
        // the run resolves those two file names against.
        string documentPath = directory.PathOf("plate.vwflow");
        shell.Chooser.AnswerSave(documentPath);
        Button(window, "_Save").Command.Execute(null);
        Settle(shell, () => session.Path == documentPath);
        Lay(window);

        session.IsDirty.ShouldBeFalse();
        shell.Status.DocumentState.ShouldBe("Saved");

        executors.Hold = false;

        Button run = Button(window, "_Run");
        run.Command.ShouldBeSameAs(shell.ViewModel.RunWorkflow.Command);
        run.Command.Execute(null);

        shell.Status.RunOutcome.ShouldBe(ShellStatus.RunningText);

        SettleRun(shell, ShellStatus.RanText);
        Lay(window);

        // What the run wrote is the image the document names, at the size the transform
        // asked for, holding the pixels the source read.
        using (Mat written = Cv2.ImRead(directory.PathOf("done.png"), ImreadModes.Color))
        {
            written.Empty().ShouldBeFalse("the run was asked to write an image beside the document.");
            written.Cols.ShouldBe(16);
            written.Rows.ShouldBe(8);
            written.At<Vec3b>(0, 0).Item0.ShouldBe((byte)120);
        }

        shell.Status.Condition.ShouldBe(ShellStatus.NoConditionText);
        shell.Preview.HasPreview.ShouldBeTrue();
        shell.Preview.PreviewTitle.ShouldBe("Resize");
        shell.Preview.PreviewDetail.ShouldBe("16 × 8 pixels, Bgr24");

        // Every node the run covered says so on the surface itself, with the time the
        // run measured for it — the path from the runner's report to the node the user
        // is looking at, driven through the window rather than asserted in pieces.
        foreach (WorkflowNodeViewModel covered in canvas.Nodes)
        {
            covered.RunState.ShouldBe(NodeRunState.Succeeded);
            covered.RunDetail.ShouldStartWith("Succeeded in ");
            covered.Summary.ShouldContain(covered.RunDetail);
        }

        WorkflowNodeViewModel drawnNode = canvas.Nodes.Single(node => node.DisplayName == "Resize");
        ItemContainer drawnContainer = Containers(window)
            .Single(container => ReferenceEquals(container.DataContext, drawnNode));

        Descendants<TextBlock>(drawnContainer)
            .ShouldContain(block => block.Text == drawnNode.RunDetail);

        Image drawn = Descendants<Image>(Named<Border>(window, "PreviewRegion")).ShouldHaveSingleItem();

        // The region draws the preview's own image, and what it draws is a frozen copy:
        // the window is redrawn on this thread long after the run gave the frame back.
        drawn.GetBindingExpression(Image.SourceProperty).ShouldNotBeNull()
            .ParentBinding.Path.Path.ShouldBe("Preview.Image");
        drawn.Source.ShouldBeAssignableTo<BitmapSource>().PixelWidth.ShouldBe(16);
        drawn.Source.ShouldBeAssignableTo<BitmapSource>().PixelHeight.ShouldBe(8);

        shell.Preview.Image.ShouldNotBeNull();
        shell.Preview.Image!.IsFrozen.ShouldBeTrue();
        waiting.Visibility.ShouldBe(Visibility.Collapsed);

        shell.Ledger.Created.ShouldBe(2);
        shell.Ledger.Released.ShouldBe(2);
        shell.Ledger.Outstanding.ShouldBe(0);
        shell.Ledger.ReservationsOutstanding.ShouldBe(0);

        // A run the user stops: the other file name is what shows a stopped run wrote
        // nothing, and the hold is what makes the stop a moment this flow can act on.
        SetParameter(shell, save.InstanceId, OpenCvNodeIds.PathParameter, "stopped.png");
        Lay(window);

        // Editing the document is what makes the marks of the run before it untrue:
        // the graph on screen is no longer the graph that ran, so a node stops claiming
        // an outcome the edit may have changed.
        canvas.Nodes.ShouldAllBe(node => node.RunState == null);
        Descendants<TextBlock>(Containers(window).First())
            .ShouldNotContain(block => block.Text == drawnNode.RunDetail);

        executors.Hold = true;

        Button cancel = Button(window, "_Cancel");
        cancel.Command.ShouldBeSameAs(shell.ViewModel.RunWorkflow.CancelCommand);
        cancel.Command.CanExecute(null).ShouldBeFalse("nothing is running yet.");

        run.Command.Execute(null);
        Settle(shell, () => executors.Started.IsCompleted);
        Lay(window);

        run.Command.CanExecute(null).ShouldBeFalse();
        cancel.Command.CanExecute(null).ShouldBeTrue();

        // The frame the source published before the run reached the held node is already
        // on screen: a stop keeps what the run produced.
        Settle(shell, () => shell.Preview.PreviewTitle == "Image Source");

        cancel.Command.Execute(null);
        SettleRun(shell, ShellStatus.StoppedText);
        Lay(window);

        executors.Hold = false;

        System.IO.File.Exists(directory.PathOf("stopped.png"))
            .ShouldBeFalse("a run stopped before its last node must not have written that node's file.");
        System.IO.File.Exists(directory.PathOf("done.png")).ShouldBeTrue();
        shell.Status.Condition.ShouldBe(ShellStatus.NoConditionText);
        shell.Preview.PreviewTitle.ShouldBe("Image Source");

        // The stopped run says where it stopped, on the surface: the node it was
        // executing is named as cancelled and the one it never reached as not run,
        // which is the reading a user wants from a graph they stopped.
        canvas.Nodes.Single(node => node.DisplayName == "Image Source").RunState.ShouldBe(NodeRunState.Succeeded);
        canvas.Nodes.Single(node => node.DisplayName == "Resize").RunState.ShouldBe(NodeRunState.Cancelled);
        canvas.Nodes.Single(node => node.DisplayName == "Resize").RunDetail.ShouldBe("Cancelled");
        canvas.Nodes.Single(node => node.DisplayName == "Save Image").RunState.ShouldBe(NodeRunState.NotRun);

        run.Command.CanExecute(null).ShouldBeTrue();
        cancel.Command.CanExecute(null).ShouldBeFalse();
        shell.Ledger.Outstanding.ShouldBe(0);
        shell.Ledger.ReservationsOutstanding.ShouldBe(0);
    }

    /// <summary>Gives a node a parameter value, which is the command a field's commit produces.</summary>
    private static void SetParameter(Shell shell, Guid nodeInstanceId, string name, object? value)
        => shell.Session
            .Execute(new SetNodeParameterCommand(nodeInstanceId, name, value))
            .IsAccepted.ShouldBeTrue($"{name} is a parameter the definition declares.");

    /// <summary>
    /// Selects the node with a given title, and only that node, the way a click on its
    /// container selects it.
    /// </summary>
    /// <param name="shell">The shell being driven.</param>
    /// <param name="displayName">The title of the node to select.</param>
    private static void Select(Shell shell, string displayName)
    {
        Guid wanted = shell.Canvas.Nodes.Single(node => node.DisplayName == displayName).InstanceId;

        foreach (ItemContainer container in Containers(shell.Window))
        {
            container.IsSelected = ((WorkflowNodeViewModel)container.DataContext).InstanceId == wanted;
        }

        Lay(shell.Window);

        shell.Session.Selection.ShouldBe(
            [wanted],
            "the surface writes a selection the session reports, so the container's own list is not a second source of truth.");
    }

    /// <summary>The parameter field the inspector is presenting.</summary>
    /// <param name="shell">The shell being driven.</param>
    /// <param name="name">The parameter name the definition declares.</param>
    /// <returns>The field's own object, which a commit refreshes rather than replaces.</returns>
    private static ParameterEditorViewModel Parameter(Shell shell, string name)
        => shell.ViewModel.Inspector.Parameters.Single(parameter => parameter.Name == name);

    /// <summary>The text field the window drew for one parameter.</summary>
    /// <param name="shell">The shell being driven.</param>
    /// <param name="parameter">The parameter the field presents.</param>
    /// <returns>The field.</returns>
    private static TextBox Field(Shell shell, ParameterEditorViewModel parameter)
        => Descendants<TextBox>(shell.Window)
            .Single(box => ReferenceEquals(box.DataContext, parameter));

    /// <summary>
    /// The gesture a text field binds: what a user presses to apply what they typed.
    /// </summary>
    /// <param name="field">The field to read the gesture of.</param>
    /// <returns>The binding, which is what the window runs on a key press.</returns>
    private static KeyBinding Gesture(TextBox field)
    {
        KeyBinding gesture = field.InputBindings.OfType<KeyBinding>().ShouldHaveSingleItem();

        gesture.Key.ShouldBe(Key.Enter);

        return gesture;
    }

    /// <summary>The node the parameters flow edits, which is the one Gaussian Blur.</summary>
    /// <param name="shell">The shell being driven.</param>
    /// <returns>The identifier the document stored it under.</returns>
    private static Guid NodeOf(Shell shell)
        => shell.Canvas.Nodes.Single(node => node.DisplayName == "Gaussian Blur").InstanceId;

    /// <summary>The value the document holds for the parameter the flow edits.</summary>
    /// <param name="shell">The shell being driven.</param>
    /// <returns>The stored value.</returns>
    private static long KernelSize(Shell shell)
        => Convert.ToInt64(
            shell.Session.Document.GetNode(NodeOf(shell)).Parameters[OpenCvNodeIds.KernelSizeParameter],
            CultureInfo.InvariantCulture);

    /// <summary>
    /// The notice a region draws over itself while the document cannot be written back,
    /// found by the reason it shows rather than by where it sits.
    /// </summary>
    /// <param name="shell">The shell being driven.</param>
    /// <param name="region">The region to read.</param>
    /// <returns>The element the notice is drawn in.</returns>
    private static FrameworkElement Notice(Shell shell, string region)
    {
        TextBlock reason = Descendants<TextBlock>(Named<Border>(shell.Window, region))
            .Single(block => block.Text == shell.ViewModel.ReadOnlyNote);

        return (FrameworkElement)reason.Parent!;
    }

    /// <summary>The list a region draws a given collection from.</summary>
    /// <param name="root">The element to search.</param>
    /// <param name="source">The collection the list presents.</param>
    /// <returns>The list.</returns>
    private static ItemsControl List(DependencyObject root, object source)
        => Descendants<ItemsControl>(root).Single(control => ReferenceEquals(control.ItemsSource, source));

    /// <summary>A named element of the window, which is how a region is reached.</summary>
    /// <typeparam name="T">The element type the name is expected to hold.</typeparam>
    /// <param name="window">The window to read.</param>
    /// <param name="name">The element's name.</param>
    /// <returns>The element.</returns>
    private static T Named<T>(Window window, string name)
        where T : FrameworkElement
    {
        object? element = window.FindName(name);

        element.ShouldNotBeNull($"{name} is an element the shell declares.");
        element.ShouldBeOfType<T>();

        return (T)element!;
    }

    /// <summary>
    /// Lets a flow that returns to the window's thread finish. Work that runs off the
    /// dispatcher is posted back to it, so waiting means running what was posted and
    /// looking again once the thread has finished a piece of it, rather than sleeping
    /// and hoping: the condition ends the wait, and the guard only ends one that would
    /// never finish, so a flow that never gets there fails with what was expected
    /// instead of hanging the test host.
    /// </summary>
    /// <param name="shell">The shell being driven.</param>
    /// <param name="until">What the flow is expected to have done.</param>
    private static void Settle(Shell shell, Func<bool> until)
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        var guard = new DispatcherTimer(DispatcherPriority.Send, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(10),
        };

        void Finished(object? sender, EventArgs args)
        {
            if (until())
            {
                frame.Continue = false;
            }
        }

        guard.Tick += (_, _) => frame.Continue = false;
        dispatcher.Hooks.OperationCompleted += Finished;
        guard.Start();

        try
        {
            if (!until())
            {
                Dispatcher.PushFrame(frame);
            }
        }
        finally
        {
            guard.Stop();
            dispatcher.Hooks.OperationCompleted -= Finished;
        }

        until().ShouldBeTrue("the flow the shell started did not finish in time.");

        // The surface the step is about to read is laid out, so it reads what a user
        // would see rather than what the last pass left pending.
        shell.Window.UpdateLayout();
    }

    /// <summary>
    /// Waits for a run the shell started to be over. The readout is written from the
    /// thread the run executes on, so it reports the outcome before the command that
    /// started the run has finished: the command being idle again is what says the
    /// gesture is done and the state it reports may be asserted.
    /// </summary>
    /// <param name="shell">The shell whose run to wait for.</param>
    /// <param name="outcome">The outcome the readout must report.</param>
    private static void SettleRun(Shell shell, string outcome)
        => Settle(
            shell,
            () => !shell.ViewModel.RunWorkflow.Command.IsRunning
                && shell.Status.RunOutcome == outcome);

    /// <summary>
    /// Lets the window lay out and apply the bindings it was just asked to draw, so a
    /// step asserts on the surface as it would look rather than on what is pending.
    /// </summary>
    /// <param name="window">The window to settle.</param>
    private static void Lay(Window window)
    {
        window.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.UpdateLayout();
    }

    private static IReadOnlyList<ItemContainer> Containers(DependencyObject root)
        => [.. Descendants<ItemContainer>(root)];

    /// <summary>
    /// The wires the surface has drawn: the drawn connections whose data context is a
    /// wire the canvas projects. The editor keeps a connection of its own for the
    /// wire a drag is drawing, and that one is not part of the document.
    /// </summary>
    /// <param name="root">The element to search.</param>
    /// <param name="canvas">The canvas the wires come from.</param>
    /// <returns>The drawn wires, in tree order.</returns>
    private static IReadOnlyList<LineConnection> Wires(DependencyObject root, CanvasViewModel canvas)
        => [.. Descendants<LineConnection>(root)
            .Where(connection => connection.DataContext is WorkflowConnectionViewModel model
                && canvas.Connectors.Contains(model))];

    /// <summary>
    /// Finds the button the shell offers for one node type or one label. A catalogue
    /// entry is commanded with the type it adds and carries its presentation as content,
    /// so the content is read only when it is the word a button shows.
    /// </summary>
    /// <param name="root">The element to search.</param>
    /// <param name="parameter">The command parameter, or the word the button shows.</param>
    /// <returns>The button.</returns>
    private static Button Button(DependencyObject root, string parameter)
        => Descendants<Button>(root).Single(button =>
            (string?)button.CommandParameter == parameter || button.Content as string == parameter);

    /// <summary>Walks the visual tree, which is where the markup's templates live.</summary>
    /// <typeparam name="T">The element type to collect.</typeparam>
    /// <param name="root">The element to search below.</param>
    /// <returns>Every matching descendant, in tree order.</returns>
    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);

        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            if (child is T wanted)
            {
                yield return wanted;
            }

            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Drags one port onto another the way the editor reports it: the drag starts at
    /// one connector, moves, and completes at the connector the pointer reached, and
    /// the editor turns the two into the command the document answers. Going through
    /// the connectors rather than the command is what keeps the pair the editor hands
    /// over — a value tuple, which its own documentation does not name — covered.
    /// </summary>
    /// <param name="window">The window the connectors are drawn in.</param>
    /// <param name="from">The port the drag starts at.</param>
    /// <param name="to">The port the drag reaches.</param>
    private static void Drag(Window window, PortViewModel from, PortViewModel to)
    {
        Connector source = Port(window, from);
        Connector target = Port(window, to);

        source.BeginConnecting();
        source.UpdatePendingConnection(target.Anchor);
        source.EndConnecting(target);
    }

    /// <summary>
    /// The connector control a port is drawn by, which is the element a drag starts
    /// at or ends on. It is found by the port presentation it carries, because that is
    /// what the editor reports as the connection's end.
    /// </summary>
    /// <param name="window">The window the connector is drawn in.</param>
    /// <param name="port">The port presentation the connector carries.</param>
    /// <returns>The connector control.</returns>
    private static Connector Port(Window window, PortViewModel port)
        => Descendants<Connector>(window).Single(connector => ReferenceEquals(connector.DataContext, port));
}
