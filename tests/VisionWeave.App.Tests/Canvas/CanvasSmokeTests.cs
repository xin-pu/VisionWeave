using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Nodify;
using Shouldly;
using VisionWeave.App.Canvas;
using VisionWeave.App.Commands;
using VisionWeave.App.Inspector;
using VisionWeave.App.Notifications;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Editing;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Nodes;
using VisionWeave.Domain.Workflows;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Tests.Canvas;

/// <summary>
/// Drives the real window: the application's theme and templates, the markup's
/// bindings, and Nodify's containers and connectors, rather than the view models
/// they are bound to. One test walks every flow the shell promises — placing,
/// connecting, selecting, deleting, and rewinding a workflow; editing a parameter,
/// reading the condition it earned, saving, and opening the file again; and then
/// opening a document this build must not write back — because the window is built
/// here under one <see cref="System.Windows.Application"/> for the process, which is
/// what WPF allows. Each step asserts on the elements a user would be looking at.
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
            var viewModel = new MainWindowViewModel(
                session,
                catalog,
                new WorkflowValidator(catalog),
                openDocument,
                new SaveDocumentCommand(boundary, session, status, chooser),
                chooser,
                new ShellPromptViewModel(),
                status);
            var window = new MainWindow(viewModel, new SnackbarNotificationPresenter(snackbar));
            CanvasViewModel canvas = viewModel.Canvas;
            Shell shell = new(window, viewModel, session, canvas, status, snackbar, chooser);

            using TemporaryWorkflowDirectory directory = new();

            window.Show();
            Lay(window);

            PlaceConnectSelectDeleteAndRewind(shell);
            EditDiagnoseSaveAndReopen(shell, directory, boundaryLog);
            BlockEditsToAnUnsupportedDocument(shell, directory);

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
    private sealed record Shell(
        Window Window,
        MainWindowViewModel ViewModel,
        EditorSession Session,
        CanvasViewModel Canvas,
        ShellStatus Status,
        RecordingSnackbarService Snackbar,
        StubFileChooser Chooser);

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

        // The editor reports where it is looking, which is what lets a node land in
        // the middle of the surface instead of somewhere off it.
        canvas.ViewportSize.Width.ShouldBeGreaterThan(0);
        canvas.ViewportSize.Height.ShouldBeGreaterThan(0);
        canvas.Nodes.ShouldBeEmpty();

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

        canvas.AddNodeCommand.Execute(OpenCvNodeIds.ResizeTypeId);
        Lay(window);

        Containers(window).Count.ShouldBe(2);

        // Adding redraws the whole surface, so the presentations are read again
        // here rather than kept from the step before.
        blur = canvas.Nodes.Single(node => node.DisplayName == "Gaussian Blur");
        WorkflowNodeViewModel resize = canvas.Nodes.Single(node => node.DisplayName == "Resize");

        // Connecting: a valid wire is drawn between the two points the ports
        // published, so it follows the ports rather than a copy of where they were.
        canvas.ConnectCommand.Execute(Dragged(blur.Outputs.ShouldHaveSingleItem(), resize.Inputs.ShouldHaveSingleItem()));
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

        canvas.ConnectCommand.Execute(Dragged(blur.Inputs.ShouldHaveSingleItem(), resize.Inputs.ShouldHaveSingleItem()));
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
    /// dispatcher is posted back to it, so waiting means running what was posted rather
    /// than sleeping on it, and a step that never finishes fails with what was expected
    /// instead of hanging.
    /// </summary>
    /// <param name="shell">The shell being driven.</param>
    /// <param name="until">What the flow is expected to have done.</param>
    private static void Settle(Shell shell, Func<bool> until)
    {
        var waited = Stopwatch.StartNew();

        while (!until() && waited.Elapsed < TimeSpan.FromSeconds(10))
        {
            Lay(shell.Window);
            Thread.Sleep(5);
        }

        until().ShouldBeTrue("the flow the shell started did not finish in time.");
    }

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

    /// <summary>Finds the button the shell offers for one node type or one label.</summary>
    /// <param name="root">The element to search.</param>
    /// <param name="parameter">The catalogue entry's command parameter, or the content.</param>
    /// <returns>The button.</returns>
    private static Button Button(DependencyObject root, string parameter)
        => Descendants<Button>(root).Single(button =>
            (string?)button.CommandParameter == parameter || (string?)button.Content == parameter);

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

    private static Tuple<object, object> Dragged(PortViewModel from, PortViewModel to)
        => Tuple.Create<object, object>(from, to);
}
