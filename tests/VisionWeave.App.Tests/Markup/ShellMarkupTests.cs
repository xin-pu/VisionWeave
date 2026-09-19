using System.CodeDom.Compiler;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using VisionWeave.App;
using VisionWeave.App.ViewModels;

namespace VisionWeave.App.Tests.Markup;

/// <summary>
/// Holds the shell's markup to what the direction documents: five named regions,
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
        "InspectorRegion",
        "StatusRegion",
    ];

    [Fact]
    public void The_shell_declares_the_five_documented_regions()
    {
        string[] regions = [.. NamedElements("MainWindow.xaml")
            .Where(name => name.EndsWith("Region", StringComparison.Ordinal))];

        regions.ShouldBe(DocumentedRegions);
    }

    [Fact]
    public void The_markup_names_only_keys_the_theme_declares()
    {
        HashSet<string> brushes = Keys("Themes/Tokens.xaml", "SolidColorBrush");
        HashSet<string> styles = Keys("Themes/Shell.xaml", "Style");

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
                styles.ShouldContain(
                    style,
                    $"{file} refers to {style}, which the shell dictionary must declare.");
            }
        }
    }

    [Fact]
    public void Every_style_the_shell_declares_is_used_by_the_shell()
    {
        // A style nothing refers to is a look the shell no longer has, and leaving it
        // behind would make the dictionary claim a state the window does not present.
        string referenced = Read("MainWindow.xaml") + Read("Themes/Shell.xaml");

        foreach (string style in Keys("Themes/Shell.xaml", "Style"))
        {
            ReferencedKeys(referenced, style).ShouldNotBeEmpty(
                $"{style} is declared but never used.");
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
    public void The_shell_binds_the_open_command_to_control_o()
    {
        XDocument shell = XDocument.Load(PathOf("MainWindow.xaml"));

        XElement binding = shell
            .Descendants()
            .Single(element => element.Name.LocalName == "KeyBinding");

        ((string?)binding.Attribute("Key")).ShouldBe("O");
        ((string?)binding.Attribute("Modifiers")).ShouldBe("Control");
        ((string?)binding.Attribute("Command")).ShouldBe("{Binding OpenCommand}");
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
        // rather than as a blank region in a running shell.
        const BindingFlags Exposed = BindingFlags.Public | BindingFlags.Instance;
        Type[] roots = [typeof(MainWindowViewModel), typeof(ShellCatalogueGroup)];

        foreach (string path in BindingPaths("MainWindow.xaml"))
        {
            string first = path.Split('.')[0];
            Type? root = roots.SingleOrDefault(candidate => candidate.GetProperty(first, Exposed) is not null);

            root.ShouldNotBeNull($"{path} starts at no type the shell presents.");

            Type current = root!;
            foreach (string step in path.Split('.'))
            {
                PropertyInfo? property = current.GetProperty(step, Exposed);

                property.ShouldNotBeNull($"{path} must resolve: {current.Name} has no public {step}.");
                current = property!.PropertyType;
            }
        }
    }

    /// <summary>
    /// Reads the paths a markup file binds, so they can be resolved without a
    /// window. A binding with no path binds the object itself and is skipped.
    /// </summary>
    private static IEnumerable<string> BindingPaths(string file)
        => Regex
            .Matches(Read(file), @"\{Binding\s+(?!RelativeSource|ElementName|Path=)([A-Za-z_][A-Za-z0-9_.]*)\}")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);

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
