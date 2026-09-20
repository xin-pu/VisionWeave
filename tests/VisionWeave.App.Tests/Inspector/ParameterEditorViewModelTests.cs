using Shouldly;
using VisionWeave.App.Inspector;
using VisionWeave.Contracts.Nodes;

namespace VisionWeave.App.Tests.Inspector;

/// <summary>
///     Covers the dedicated controls exposed by a parameter editor independently of
///     the window that renders them.
/// </summary>
public sealed class ParameterEditorViewModelTests
{
    [Fact]
    public void Numeric_value_changed_commits_the_invariant_text_value()
    {
        ParameterEditorViewModel editor = Editor(new ParameterDefinition(
            "amount",
            ParameterKind.Integer,
            IsRequired: true,
            DisplayName: "Amount",
            Minimum: 1,
            Maximum: 99,
            DefaultValue: 5));
        int commits = 0;
        editor.Commit = _ => commits++;

        editor.NumericValue = 7;

        editor.Text.ShouldBe("7");
        editor.IsNumeric.ShouldBeTrue();
        editor.Minimum.ShouldBe(1);
        editor.Maximum.ShouldBe(99);
        editor.SmallChange.ShouldBe(1);
        commits.ShouldBe(1);
    }

    [Fact]
    public void Browse_selected_path_commits_it_once()
    {
        var chooser = new StubPathChooser(new ParameterPathChoice("images\\output.png", null));
        ParameterEditorViewModel editor = Editor(
            new ParameterDefinition(
                "path",
                ParameterKind.Path,
                IsRequired: true,
                DisplayName: "File",
                PathSelection: PathSelectionMode.SaveFile),
            chooser);
        int commits = 0;
        editor.Commit = _ => commits++;

        editor.BrowseCommand.Execute(null);

        chooser.Mode.ShouldBe(PathSelectionMode.SaveFile);
        chooser.WorkingDirectory.ShouldBe("D:\\project");
        editor.Text.ShouldBe("images\\output.png");
        commits.ShouldBe(1);
    }

    [Fact]
    public void Browse_dismissed_leaves_the_path_unchanged()
    {
        var chooser = new StubPathChooser(ParameterPathChoice.Dismissed);
        ParameterEditorViewModel editor = Editor(
            new ParameterDefinition(
                "path",
                ParameterKind.Path,
                IsRequired: true,
                DisplayName: "Folder",
                DefaultValue: "D:\\images",
                PathSelection: PathSelectionMode.Folder),
            chooser);
        int commits = 0;
        editor.Commit = _ => commits++;

        editor.BrowseCommand.Execute(null);

        editor.Text.ShouldBe("D:\\images");
        commits.ShouldBe(0);
    }

    [Fact]
    public void PortablePath_inside_workflow_directory_becomes_relative()
    {
        ParameterPathChoice choice = WindowsParameterPathChooser.PortablePath(
            "D:\\project\\images\\input.png",
            "D:\\project");

        choice.Path.ShouldBe("images\\input.png");
        choice.Refusal.ShouldBeNull();
    }

    [Fact]
    public void PortablePath_outside_workflow_directory_is_refused()
    {
        ParameterPathChoice choice = WindowsParameterPathChooser.PortablePath(
            "D:\\shared\\input.png",
            "D:\\project");

        choice.Path.ShouldBeNull();
        choice.Refusal.ShouldNotBeNull().ShouldContain("inside the workflow directory");
    }

    private static ParameterEditorViewModel Editor(
        ParameterDefinition definition,
        IParameterPathChooser? chooser = null)
        => new(definition, null, null, string.Empty, chooser, () => "D:\\project");

    private sealed class StubPathChooser(ParameterPathChoice answer) : IParameterPathChooser
    {
        internal PathSelectionMode? Mode { get; private set; }

        internal string? WorkingDirectory { get; private set; }

        public ParameterPathChoice ChoosePath(
            PathSelectionMode mode,
            string? currentPath,
            string? workingDirectory)
        {
            Mode = mode;
            WorkingDirectory = workingDirectory;
            return answer;
        }
    }
}
