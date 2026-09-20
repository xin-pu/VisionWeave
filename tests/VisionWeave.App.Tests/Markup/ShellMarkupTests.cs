using System.CodeDom.Compiler;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using VisionWeave.App;
using VisionWeave.App.Canvas;
using VisionWeave.App.Commands;
using VisionWeave.App.Inspector;
using VisionWeave.App.Preview;
using VisionWeave.App.ViewModels;
using VisionWeave.Application.Execution;

namespace VisionWeave.App.Tests.Markup;

/// <summary>
/// Holds the shell's markup to what the direction documents: six named regions,
/// semantic brush names instead of colours, the framework theme merged ahead of the
/// shell's dictionaries, keyboard focus on the one action the shell offers, the
/// region a snackbar is drawn in, and design-time content. The markup is read as
/// data, so the structure is covered without creating a window.
/// </summary>
public sealed class ShellMarkupTests
{
    /// <summary>The regions the direction documents, in the order they are laid out.</summary>
    private static readonly string[] DocumentedRegions =
    [
        "TopBarRegion",
        "NodeCatalogueRegion",
        "CanvasRegion",
        "PreviewRegion",
        "InspectorRegion",
        "StatusRegion",
    ];

    /// <summary>
    /// The objects the shell's markup binds against: the window's view model, the
    /// regions it presents — the status area, the inspector, the preview, and the
    /// prompt — the command the run actions share, the records the catalogue is
    /// drawn from, and the types the canvas and inspector templates take as their
    /// data context.
    /// </summary>
    private static readonly Type[] BindingRoots =
    [
        typeof(MainWindowViewModel),
        typeof(ShellStatus),
        typeof(InspectorViewModel),
        typeof(ParameterEditorViewModel),
        typeof(DiagnosticEntryViewModel),
        typeof(ShellPromptViewModel),
        typeof(RunWorkflowCommand),
        typeof(PreviewViewModel),
        typeof(ShellCatalogueGroup),
        typeof(ShellCatalogueEntry),
        typeof(WorkflowNodeViewModel),
        typeof(PortViewModel),
        typeof(WorkflowConnectionViewModel),
    ];

    [Fact]
    public void The_shell_declares_the_six_documented_regions()
    {
        string[] regions = [.. NamedElements("MainWindow.xaml")
            .Where(name => name.EndsWith("Region", StringComparison.Ordinal))];

        regions.ShouldBe(DocumentedRegions);
    }

    [Fact]
    public void The_markup_names_only_keys_the_theme_declares()
    {
        HashSet<string> brushes = Keys("Themes/Tokens.xaml", "SolidColorBrush");
        HashSet<string> declared = DeclaredKeys();

        foreach (string file in new[] { "App.xaml", "MainWindow.xaml", "Themes/Shell.xaml" })
        {
            string markup = Read(file);

            foreach (string brush in ReferencedKeys(markup, "Brush."))
            {
                brushes.ShouldContain(
                    brush,
                    $"{file} refers to {brush}, which the token dictionary must declare.");
            }

            foreach (string style in ReferencedKeys(markup, "Shell."))
            {
                declared.ShouldContain(
                    style,
                    $"{file} refers to {style}, which the shell dictionary must declare.");
            }
        }
    }

    [Fact]
    public void Every_key_the_shell_declares_is_used_by_the_shell()
    {
        // A key nothing refers to is a look the shell no longer has, and leaving it
        // behind would make the dictionary claim a state the window does not present.
        string referenced = Read("MainWindow.xaml") + Read("Themes/Shell.xaml");

        foreach (string key in DeclaredKeys())
        {
            ReferencedKeys(referenced, key).ShouldNotBeEmpty(
                $"{key} is declared but never used.");
        }
    }

    [Fact]
    public void The_shell_declares_every_key_it_reaches_for_before_it_reaches_for_it()
    {
        // A StaticResource is resolved against what the dictionary has already
        // declared, so a template that reaches for a key declared below it fails the
        // moment the template is applied — while the shell draws its first node,
        // rather than here.
        HashSet<string> declared = [];

        foreach (XElement entry in XDocument.Load(PathOf("Themes/Shell.xaml")).Root!.Elements())
        {
            foreach (string key in ReferencedKeys(entry.ToString(), "Shell."))
            {
                declared.ShouldContain(
                    key,
                    $"{entry.Name.LocalName} {((string?)entry.Attribute(Xaml + "Key")) ?? "?"} reaches for {key} before the dictionary declares it.");
            }

            if (entry.Attribute(Xaml + "Key") is { } declaredKey)
            {
                declared.Add((string)declaredKey);
            }
        }
    }

    [Fact]
    public void The_application_merges_the_framework_theme_before_the_shell_dictionaries()
    {
        string[] sources =
        [
            .. XDocument
                .Load(PathOf("App.xaml"))
                .Descendants()
                // The property is written as a property element, so its name carries
                // the owning type in front of the property name.
                .Single(element => element.Name.LocalName.EndsWith("MergedDictionaries", StringComparison.Ordinal))
                .Elements()
                .Select(element => ((string?)element.Attribute("Source")) ?? FrameworkNamespace),
        ];

        sources.ShouldBe(
        [
            // The framework dictionaries arrive first, so the shell's tokens replace
            // the values they declare instead of being replaced by them.
            FrameworkNamespace,
            FrameworkNamespace,
            "Themes/Tokens.xaml",
            "Themes/Shell.xaml",
        ]);
    }

    [Fact]
    public void The_shell_binds_the_editing_gestures_to_the_documented_shortcuts()
    {
        Dictionary<string, (string? Modifiers, string? Command)> shortcuts = XDocument
            .Load(PathOf("MainWindow.xaml"))
            .Descendants()
            .Where(element => element.Name.LocalName == "KeyBinding")
            .ToDictionary(
                element => (string)element.Attribute("Key")!,
                element => ((string?)element.Attribute("Modifiers"), (string?)element.Attribute("Command")));

        // The window's own shortcuts: opening a file, writing it, running it, and the
        // two history steps every editor offers.
        shortcuts["O"].ShouldBe(("Control", "{Binding OpenCommand}"));
        shortcuts["S"].ShouldBe(("Control", "{Binding SaveCommand}"));
        shortcuts["R"].ShouldBe(("Control", "{Binding RunWorkflow.Command}"));
        shortcuts["Z"].ShouldBe(("Control", "{Binding Canvas.UndoCommand}"));
        shortcuts["Y"].ShouldBe(("Control", "{Binding Canvas.RedoCommand}"));

        // Deleting belongs to the work surface, so it is bound where the focus is
        // rather than on the window.
        shortcuts["Delete"].ShouldBe((null, "{Binding Canvas.DeleteSelectionCommand}"));
    }

    [Fact]
    public void The_catalogue_offers_a_search_and_says_what_it_matched()
    {
        XElement catalogue = Region("NodeCatalogueRegion");

        // The search is a query over the catalog, so it is bound to the state the view
        // model derives rather than to a filtered copy the region keeps, and it reports
        // every keystroke as it is typed.
        XElement search = catalogue
            .Descendants()
            .Single(element => ((string?)element.Attribute("Text"))?.StartsWith("{Binding CatalogueSearch", StringComparison.Ordinal) == true);
        AttributeText(search, "Text").ShouldBe("{Binding CatalogueSearch, UpdateSourceTrigger=PropertyChanged}");

        Texts(catalogue).ShouldContain("{Binding NodeCatalogSummary}");
        Texts(catalogue).ShouldContain("{Binding CatalogueNotice}");

        // Choosing a type reaches the canvas command, and a document this build cannot
        // write back offers no addable type at all.
        XElement entry = catalogue
            .Descendants()
            .Single(element => element.Name.LocalName == "Button" && (string?)element.Attribute("Command") is not null);
        AttributeText(entry, "Command").ShouldContain("Canvas.AddNodeCommand");
        AttributeText(entry, "IsEnabled").ShouldContain("Canvas.IsEditable");
    }

    [Fact]
    public void The_inspector_presents_the_selection_its_fields_and_its_conditions()
    {
        XElement inspector = Region("InspectorRegion");

        Texts(inspector).ShouldContain("{Binding Inspector.NodeTitle}");
        Texts(inspector).ShouldContain("{Binding Inspector.NodeCaption}");
        Texts(inspector).ShouldContain("{Binding Inspector.ParameterNote}");
        Texts(inspector).ShouldContain("{Binding Inspector.DiagnosticCountText}");

        // The fields are the declaration's, so the region repeats one template rather
        // than naming a parameter, and the whole list is disabled at once when the
        // document may not be edited.
        AttributeText(List(inspector, "{Binding Inspector.Parameters}"), "IsEnabled")
            .ShouldBe("{Binding Inspector.IsEditable}");
        List(inspector, "{Binding Inspector.Diagnostics}").ShouldNotBeNull();
    }

    [Fact]
    public void A_parameter_field_offers_one_projection_for_the_kind_it_was_declared_with()
    {
        XElement inspector = Region("InspectorRegion");

        // Text, a switch, and a list of options are three projections of one
        // parameter; the declared kind picks one of them, so the region never shows
        // two ways of editing the same value.
        string markup = inspector.ToString();

        markup.ShouldContain("IsBoolean");
        markup.ShouldContain("HasOptions");
        markup.ShouldContain("{Binding IsChecked}");
        markup.ShouldContain("{Binding SelectedOption}");
        markup.ShouldContain("{Binding Options}");

        // A value that has to be typed in is applied with Enter. The gesture is
        // written inside the field's own template, so it resolves against the field
        // rather than against the window, and it asks the field — not the inspector —
        // to commit what it holds.
        XElement keys = inspector
            .Descendants()
            .Single(element => element.Name.LocalName.EndsWith(".InputBindings", StringComparison.Ordinal));

        keys.Name.LocalName.ShouldBe("TextBox.InputBindings");

        XElement gesture = keys.Elements().ShouldHaveSingleItem();
        gesture.Name.LocalName.ShouldBe("KeyBinding");
        AttributeText(gesture, "Key").ShouldBe("Enter");
        AttributeText(gesture, "Command").ShouldBe("{Binding ApplyCommand}");
        gesture.Attribute("Modifiers").ShouldBeNull();
    }

    [Fact]
    public void The_shell_shows_why_a_document_it_cannot_write_back_reads_only()
    {
        // Both the surface the document is edited on and the panel its parameters are
        // edited in say the same thing, because both refuse the same edits: the notice
        // is drawn while the document is read-only and stays out of the way otherwise.
        foreach (string region in new[] { "CanvasRegion", "InspectorRegion" })
        {
            string notice = Region(region)
                .Descendants()
                .Single(element => element.Name.LocalName == "TextBlock"
                    && (string?)element.Attribute("Text") == "{Binding ReadOnlyNote}")
                .Parent!
                .ToString();

            notice.ShouldContain("{Binding IsReadOnly}");
            notice.ShouldContain("Collapsed");
        }
    }

    [Fact]
    public void The_header_runs_the_document_and_stops_the_run()
    {
        XElement topBar = Region("TopBarRegion");
        string[] commands =
        [
            .. topBar
                .Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .Select(element => AttributeText(element, "Command")),
        ];

        // Running and stopping are one object seen through two commands: the Run
        // action's own command is disabled while a run executes, and the Cancel
        // action's is enabled exactly while there is a run to stop.
        commands.ShouldContain("{Binding OpenCommand}");
        commands.ShouldContain("{Binding SaveCommand}");
        commands.ShouldContain("{Binding RunWorkflow.Command}");
        commands.ShouldContain("{Binding RunWorkflow.CancelCommand}");

        // What the last run did is reported where the document commands are, because
        // it is a fact about the document rather than about one region of it.
        Texts(topBar).ShouldContain("{Binding Status.RunOutcome}");
    }

    [Fact]
    public void The_preview_region_draws_the_newest_image_a_run_published()
    {
        XElement preview = Region("PreviewRegion");

        // The preview belongs to the work surface: it is drawn inside the canvas
        // region and below the editor, so a document keeps the room it is edited in.
        Region("CanvasRegion")
            .Descendants()
            .ShouldContain(element => (string?)element.Attribute(Xaml + "Name") == "PreviewRegion");

        Texts(preview).ShouldContain("{Binding Preview.PreviewTitle}");
        Texts(preview).ShouldContain("{Binding Preview.PreviewDetail}");
        Texts(preview).ShouldContain("{Binding Preview.PreviewNotice}");

        XElement image = preview
            .Descendants()
            .Single(element => element.Name.LocalName == "Image");

        AttributeText(image, "Source").ShouldBe("{Binding Preview.Image}");

        // The notice is the one part of the region that is a state rather than a
        // value, so it is the part that shows and hides with the state behind it.
        XElement notice = preview
            .Descendants()
            .Single(element => (string?)element.Attribute("Text") == "{Binding Preview.PreviewNotice}");

        notice.ToString().ShouldContain("{Binding Preview.HasPreview}");
        notice.ToString().ShouldContain("Collapsed");
    }

    [Fact]
    public void The_shell_asks_a_question_over_the_work_surface()
    {
        XElement overlay = XDocument
            .Load(PathOf("MainWindow.xaml"))
            .Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "PromptOverlay");

        // The question is drawn over the shell rather than in a window of its own, so
        // the flow that asks and the surface that shows are the same object.
        overlay.ToString().ShouldContain("{Binding Prompt.IsOpen}");
        overlay.ToString().ShouldContain("{Binding Prompt.Question}");

        string[] buttons =
        [
            .. overlay
                .Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .Select(element => AttributeText(element, "Command")),
        ];

        buttons.ShouldBe(
        [
            "{Binding Prompt.AcceptCommand}",
            "{Binding Prompt.RefuseCommand}",
            "{Binding Prompt.DismissCommand}",
        ]);

        // The three buttons carry the words the question offered rather than words the
        // markup chose, because which answer means what is the asking flow's decision.
        string[] labels =
        [
            .. overlay
                .Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .Select(element => AttributeText(element, "Content")),
        ];

        labels.ShouldBe(
        [
            "{Binding Prompt.AcceptText}",
            "{Binding Prompt.RefuseText}",
            "{Binding Prompt.CancelText}",
        ]);
    }

    [Fact]
    public void The_node_template_states_what_the_newest_run_did_to_the_node()
    {
        XElement node = Template("Shell.CanvasNode");

        // The mark is written on the node rather than only into the status area, so a
        // run that fails halfway says where on the surface the user is looking.
        node.Descendants().ShouldContain(element => (string?)element.Attribute("Text") == "{Binding RunDetail}");

        // It reads by its word and only then by its colour, so every terminal state a
        // run can report has a mark of its own, and a node no run covered shows none.
        string markup = node.ToString();

        markup.ShouldContain("{Binding RunState}");

        foreach (NodeRunState state in Enum.GetValues<NodeRunState>())
        {
            markup.ShouldContain($"Value=\"{state}\"");
        }

        markup.ShouldContain("Collapsed");
    }

    /// <summary>The resource entry the theme declares under a key.</summary>
    /// <param name="key">The key to read.</param>
    /// <returns>The element.</returns>
    private static XElement Template(string key)
        => XDocument
            .Load(PathOf("Themes/Shell.xaml"))
            .Root!
            .Elements()
            .Single(entry => (string?)entry.Attribute(Xaml + "Key") == key);

    /// <summary>The named region whose contents a test reads.</summary>
    /// <param name="name">The region's name.</param>
    /// <returns>The element.</returns>
    private static XElement Region(string name)
        => XDocument
            .Load(PathOf("MainWindow.xaml"))
            .Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Name") == name);

    /// <summary>Reads the text a region's elements show, so a test names what is presented.</summary>
    /// <param name="region">The region to read.</param>
    /// <returns>The Text attribute of every element that carries one.</returns>
    private static string[] Texts(XElement region)
        => [.. region.Descendants().Select(element => (string?)element.Attribute("Text")).OfType<string>()];

    /// <summary>Reads the control a region presents a binding path as its ItemsSource.</summary>
    /// <param name="region">The region to read.</param>
    /// <param name="path">The binding path the control presents.</param>
    /// <returns>The element.</returns>
    private static XElement List(XElement region, string path)
        => region
            .Descendants()
            .Single(element => (string?)element.Attribute("ItemsSource") == path);

    [Fact]
    public void The_canvas_presents_the_projection_and_reports_every_gesture_as_a_command()
    {
        XElement canvas = NodifyEditor();

        // What the document decides is presented here; every gesture the work
        // surface recognizes leaves as a command, so the markup holds no rule of
        // its own about what may be placed, connected, or removed.
        AttributeText(canvas, "ItemsSource").ShouldBe("{Binding Canvas.Nodes}");
        AttributeText(canvas, "Connections").ShouldBe("{Binding Canvas.Connectors}");
        AttributeText(canvas, "ConnectionCompletedCommand").ShouldBe("{Binding Canvas.ConnectCommand}");
        AttributeText(canvas, "RemoveConnectionCommand").ShouldBe("{Binding Canvas.DisconnectCommand}");
        AttributeText(canvas, "ItemsDragCompletedCommand").ShouldBe("{Binding Canvas.MoveCommand}");
        AttributeText(canvas, "ItemContainerStyle").ShouldBe("{StaticResource Shell.CanvasNodeContainer}");
        AttributeText(canvas, "ItemTemplate").ShouldBe("{StaticResource Shell.CanvasNode}");
        AttributeText(canvas, "ConnectionTemplate").ShouldBe("{StaticResource Shell.CanvasConnectionTemplate}");
        AttributeText(canvas, "PendingConnectionTemplate").ShouldBe("{StaticResource Shell.CanvasPendingConnection}");

        // A node is placed where the user is looking, so the surface reports where
        // its viewport is instead of the canvas asking the window for it.
        AttributeText(canvas, "ViewportLocation").ShouldBe("{Binding Canvas.ViewportLocation, Mode=OneWayToSource}");
        AttributeText(canvas, "ViewportSize").ShouldBe("{Binding Canvas.ViewportSize, Mode=OneWayToSource}");
    }

    [Fact]
    public void The_shell_hosts_one_region_to_draw_an_announcement_in()
    {
        IReadOnlyList<XElement> hosts = [.. XDocument
            .Load(PathOf("MainWindow.xaml"))
            .Descendants()
            .Where(element => element.Name.LocalName == "SnackbarPresenter")];

        // One host, named, so the window can attach the presenter to exactly the
        // region the markup lays out.
        hosts.ShouldHaveSingleItem().Attribute(Xaml + "Name")?.Value.ShouldBe("SnackbarHost");
    }

    [Fact]
    public void The_shell_presents_design_time_content()
    {
        XDocument shell = XDocument.Load(PathOf("MainWindow.xaml"));
        XNamespace design = "http://schemas.microsoft.com/expression/blend/2008";

        ((string?)shell.Root!.Attribute(design + "DataContext")).ShouldBe(
            "{x:Static design:ShellDesignData.ViewModel}");

        // The sample is only shown by a designer, so the markup must declare the
        // attribute ignorable for the application itself to load it.
        ((string?)shell.Root!.Attribute(Compatibility + "Ignorable")).ShouldBe("d");
    }

    [Fact]
    public void The_window_code_behind_declares_no_behaviour_of_its_own()
    {
        // The window may initialize its markup and hand over the objects it was
        // built with. Anything else here would be a rule that lives outside the
        // application commands, which is what the shell's boundary forbids.
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        string[] declared =
        [
            .. typeof(MainWindow)
                .GetMethods(Declared)
                .Where(method => !method.IsConstructor)
                // The markup compiler generates the two members that build the
                // window and connect its named elements to it.
                .Where(method => !method.GetCustomAttributes<GeneratedCodeAttribute>(inherit: false).Any())
                .Select(method => method.Name),
            .. typeof(MainWindow)
                .GetProperties(Declared)
                .Select(property => property.Name),
            .. typeof(MainWindow)
                .GetEvents(Declared)
                .Select(@event => @event.Name),
        ];

        declared.ShouldBeEmpty("the code-behind must not carry behaviour of its own.");
    }

    [Fact]
    public void Every_binding_the_shell_uses_resolves_through_a_public_member()
    {
        // WPF binds only to public members, and a binding to anything else fails
        // silently: the field is empty and nothing reports an error. Every path the
        // markup binds is therefore walked here from the object the markup binds it
        // against, so a member that loses its accessibility fails in this test
        // rather than as a blank region in a running shell. A path such as
        // DisplayName is presented by more than one object, so it needs one root
        // that carries the whole path.
        foreach (string file in new[] { "MainWindow.xaml", "Themes/Shell.xaml" })
        {
            foreach (string path in BindingPaths(file))
            {
                string[] steps = path.Split('.');

                BindingRoots
                    .Where(root => Resolves(root, steps))
                    .ShouldNotBeEmpty(
                        $"{file} binds {path}, which resolves from no type the shell presents.");
            }
        }
    }

    /// <summary>
    /// Walks a binding path from a candidate root, so the whole chain is checked
    /// rather than only its first step.
    /// </summary>
    /// <param name="root">The object the markup could be binding against.</param>
    /// <param name="steps">The path, split on its separators.</param>
    /// <returns><see langword="true"/> when the path resolves.</returns>
    private static bool Resolves(Type root, IReadOnlyList<string> steps)
    {
        const BindingFlags Exposed = BindingFlags.Public | BindingFlags.Instance;
        Type? current = root;

        foreach (string step in steps)
        {
            current = current.GetProperty(step, Exposed)?.PropertyType;

            if (current is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads the paths a markup file binds, so they can be resolved without a
    /// window. A binding that names its own source is left out: the framework
    /// resolves it against an object this test cannot walk to.
    /// </summary>
    private static IEnumerable<string> BindingPaths(string file)
        => Regex
            .Matches(Read(file), @"\{Binding\s+([^{}]*)\}")
            .Select(match => match.Groups[1].Value)
            .Where(body => !body.Contains("RelativeSource", StringComparison.Ordinal)
                && !body.Contains("ElementName", StringComparison.Ordinal))
            .Select(body => body.Split(',')[0].Trim().Replace("Path=", string.Empty, StringComparison.Ordinal))
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.Ordinal);

    /// <summary>The work surface the canvas region draws.</summary>
    private static XElement NodifyEditor()
        => XDocument
            .Load(PathOf("MainWindow.xaml"))
            .Descendants()
            .Single(element => element.Name.LocalName == "NodifyEditor");

    /// <summary>Reads an attribute the markup is expected to carry.</summary>
    /// <param name="element">The element to read.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The value, which fails the test when the attribute is absent.</returns>
    private static string AttributeText(XElement element, string name)
    {
        string? value = (string?)element.Attribute(name);

        return value.ShouldNotBeNull($"{element.Name.LocalName} declares no {name}.");
    }

    /// <summary>
    /// Every key the shell dictionary declares. The markup reaches for styles and
    /// templates by name, so both are keys the theme promises.
    /// </summary>
    private static HashSet<string> DeclaredKeys()
        => [.. Keys("Themes/Shell.xaml", "Style"), .. Keys("Themes/Shell.xaml", "DataTemplate")];

    private static IEnumerable<string> NamedElements(string file)
        => XDocument
            .Load(PathOf(file))
            .Descendants()
            .Select(element => (string?)element.Attribute(Xaml + "Name"))
            .Where(name => name is not null)
            .Select(name => name!);

    private static HashSet<string> Keys(string file, string element)
        => [.. XDocument
            .Load(PathOf(file))
            .Root!
            .Elements()
            .Where(entry => entry.Name.LocalName == element)
            .Select(entry => (string)entry.Attribute(Xaml + "Key")!)];

    /// <summary>
    /// Finds every resource name a markup file refers to under a prefix, so a name
    /// the theme does not declare fails here rather than as a blank surface.
    /// </summary>
    private static IEnumerable<string> ReferencedKeys(string markup, string prefix)
        => Regex
            .Matches(markup, $@"(?:Dynamic|Static)Resource\s+({Regex.Escape(prefix)}[A-Za-z0-9.]*)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);

    private static string Read(string file) => System.IO.File.ReadAllText(PathOf(file));

    /// <summary>
    /// Resolves a shell file the test project links into its output, so the markup
    /// is checked against the files the application actually ships.
    /// </summary>
    private static string PathOf(string file)
        => System.IO.Path.Combine(AppContext.BaseDirectory, "Shell", file);

    private static XNamespace Xaml => "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XNamespace Compatibility => "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private const string FrameworkNamespace = "http://schemas.lepo.co/wpfui/2022/xaml";
}
