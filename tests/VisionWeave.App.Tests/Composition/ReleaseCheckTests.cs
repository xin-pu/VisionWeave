using System.Xml.Linq;
using Shouldly;
using VisionWeave.App.Composition;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Tests.Composition;

/// <summary>
/// Covers the one thing a published build can be asked to answer on its own:
/// <c>VisionWeave.App.exe --check</c>. The line and the exit code it produces are a
/// package's only self-description on a machine that cannot see a window, so both
/// are asserted here, and CI runs the same switch against the folder the publish
/// produced rather than against this project's build output.
/// </summary>
public sealed class ReleaseCheckTests : IDisposable
{
    private readonly string _emptyDirectory =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"visionweave-check-{Guid.NewGuid():N}");

    public ReleaseCheckTests() => System.IO.Directory.CreateDirectory(_emptyDirectory);

    [Fact]
    public void IsRequested_a_command_line_carrying_the_switch_asks_for_the_check()
    {
        ReleaseCheck.IsRequested(["--check"]).ShouldBeTrue();
    }

    [Fact]
    public void IsRequested_an_ordinary_command_line_asks_for_the_shell()
    {
        ReleaseCheck.IsRequested([]).ShouldBeFalse();
        ReleaseCheck.IsRequested(["--something-else"]).ShouldBeFalse();
    }

    [Fact]
    public void IsRequested_a_switch_that_only_starts_with_the_word_is_not_the_check()
    {
        ReleaseCheck.IsRequested(["--checkpoint"]).ShouldBeFalse();
    }

    [Fact]
    public void IsRequested_the_switch_ignores_the_case_it_was_typed_in()
    {
        ReleaseCheck.IsRequested(["--CHECK"]).ShouldBeTrue();
    }

    [Fact]
    public void Run_a_folder_without_settings_writes_one_line_and_refuses_with_a_code()
    {
        using var output = new System.IO.StringWriter();

        int code = ReleaseCheck.Run(output, _emptyDirectory);

        code.ShouldBe((int)ReleaseCheckExitCode.SettingsRejected);
        SingleLine(output).ShouldContain(DiagnosticCodes.InvalidSetting);
    }

    [Fact]
    public void Run_the_deployment_folder_composes_the_host_and_runs_a_native_operation()
    {
        using var output = new System.IO.StringWriter();

        int code = ReleaseCheck.Run(output, AppContext.BaseDirectory);

        code.ShouldBe((int)ReleaseCheckExitCode.Passed);

        string line = SingleLine(output);

        // Composition succeeded, the catalog contributed its OpenCV types, and a
        // frame went through the native threshold call and back out as pixels.
        line.ShouldContain("check passed");
        line.ShouldContain("node types");
        line.ShouldContain("published 255");
        line.ShouldContain(DeclaredVersion());
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_emptyDirectory))
        {
            System.IO.Directory.Delete(_emptyDirectory, recursive: true);
        }
    }

    private static string SingleLine(System.IO.StringWriter output)
    {
        string[] lines = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();

        lines.ShouldHaveSingleItem("a check reports exactly one line, whatever it found.");

        return lines[0];
    }

    /// <summary>
    /// Reads the version the repository states once, which is the version the
    /// publish names the folder after. A build whose assemblies reported another
    /// one would hand out a package that misnames itself.
    /// </summary>
    private static string DeclaredVersion()
    {
        string propsPath = System.IO.Path.Combine(RepositoryRoot(), "Directory.Build.props");

        return XDocument.Load(propsPath)
            .Descendants("Version")
            .First()
            .Value;
    }

    private static string RepositoryRoot()
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "VisionWeave.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"The repository root was not found above '{AppContext.BaseDirectory}'.");
    }
}
