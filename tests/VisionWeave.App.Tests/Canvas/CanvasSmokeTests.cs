using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Nodify;
using Shouldly;
using VisionWeave.App.Canvas;
using VisionWeave.App.Commands;
using VisionWeave.App.Notifications;
using VisionWeave.App.Sessions;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Definitions;
using VisionWeave.Application.Validation;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Tests.Canvas;

/// <summary>
/// Drives the real window: the application's theme and templates, the markup's
/// bindings, and Nodify's containers and connectors, rather than the view models
/// they are bound to. One test walks the whole flow the package promises — place,
/// connect, select, delete, undo, redo, and a refused connection that leaves the
/// surface exactly as it was — because the window is built here under one
/// <see cref="System.Windows.Application"/> for the process, which is what WPF
/// allows. Each step asserts on the elements a user would be looking at.
/// </summary>
public sealed class CanvasSmokeTests
{
    [Fact]
    public void The_shell_places_connects_selects_deletes_and_rewinds_a_workflow_on_the_canvas()
        => StaThread.Run(() =>
        {
            // The window is built from the application's own resources: the framework
            // theme, the tokens, and the shell styles are the ones that ship.
            var application = new App();
            application.InitializeComponent();

            NodeDefinitionCatalog catalog = NodeDefinitionCatalog.FromProviders([new OpenCvNodeDefinitionProvider()]);
            EditorSession session = TestSessions.Create(catalog: catalog);
            ShellStatus status = new(session);
            RecordingSnackbarService snackbar = new();
            var openDocument = new OpenDocumentCommand(
                new AsyncCommandBoundary(new RecordingNotificationPresenter(), new RecordingLogger<AsyncCommandBoundary>()),
                session,
                status);
            var viewModel = new MainWindowViewModel(
                session,
                catalog,
                new WorkflowValidator(catalog),
                openDocument,
                new StubFileChooser(),
                status);
            var window = new MainWindow(viewModel, new SnackbarNotificationPresenter(snackbar));
            CanvasViewModel canvas = viewModel.Canvas;

            window.Show();
            Lay(window);

            // The editor reports where it is looking, which is what lets a node land in
            // the middle of the surface instead of somewhere off it.
            canvas.ViewportSize.Width.ShouldBeGreaterThan(0);
            canvas.ViewportSize.Height.ShouldBeGreaterThan(0);
            canvas.Nodes.ShouldBeEmpty();

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

            window.Close();
        });

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
