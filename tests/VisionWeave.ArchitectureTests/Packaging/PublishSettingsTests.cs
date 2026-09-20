using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Shouldly;

namespace VisionWeave.ArchitectureTests.Packaging;

/// <summary>
/// Holds the two halves of a package together: what the repository declares a
/// package is, and what the script that produces one actually runs. The script
/// reads the declaration instead of restating it, so what is checked here is that
/// the plan it states carries those declared values into the publish command — a
/// drift between the two would hand out a folder that is not the one the script
/// claims to make.
/// </summary>
public sealed class PublishSettingsTests
{
    [Fact]
    public void The_publish_script_runs_the_settings_the_repository_declares()
    {
        string repositoryRoot = RepositoryRoot();
        Declared declared = Declared.Read(repositoryRoot);

        using JsonDocument plan = JsonDocument.Parse(RunPlan(repositoryRoot));
        JsonElement resolved = plan.RootElement;

        resolved.GetProperty("version").GetString().ShouldBe(declared.Version);
        resolved.GetProperty("project").GetString().ShouldBe(declared.Project);
        resolved.GetProperty("configuration").GetString().ShouldBe(declared.Configuration);
        resolved.GetProperty("runtimeIdentifier").GetString().ShouldBe(declared.RuntimeIdentifier);
        resolved.GetProperty("selfContained").GetBoolean().ShouldBe(declared.SelfContained);
        resolved.GetProperty("outputRoot").GetString().ShouldBe(declared.OutputRoot);

        // The folder a user downloads names the version and the runtime it holds.
        resolved.GetProperty("outputDirectory").GetString()
            .ShouldBe($"{declared.OutputRoot}/VisionWeave-{declared.Version}-{declared.RuntimeIdentifier}");

        string command = resolved.GetProperty("command").GetString().ShouldNotBeNull();

        command.ShouldContain("dotnet publish");
        command.ShouldContain($"--configuration {declared.Configuration}");
        command.ShouldContain($"--runtime {declared.RuntimeIdentifier}");
        command.ShouldContain($"--self-contained {declared.SelfContained.ToString().ToLowerInvariant()}");
        command.ShouldContain("--output");
    }

    [Fact]
    public void The_declared_package_is_the_self_contained_windows_folder_the_decision_chose()
    {
        Declared declared = Declared.Read(RepositoryRoot());

        // ADR-0014: the folder holds the runtime, so it starts on a clean Windows
        // host with nothing installed. Changing any of the three moves the failure
        // back onto the machine that runs it, which is a decision to make on purpose.
        declared.Configuration.ShouldBe("Release");
        declared.RuntimeIdentifier.ShouldBe("win-x64");
        declared.SelfContained.ShouldBeTrue();
    }

    [Fact]
    public void The_version_is_stated_once_for_the_whole_repository()
    {
        string repositoryRoot = RepositoryRoot();
        string declaredVersion = Declared.Read(repositoryRoot).Version;
        List<string> offenders = [];

        foreach (string project in Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(project))
            {
                continue;
            }

            bool statesVersion = XDocument.Load(project).Descendants().Any(element =>
                element.Name.LocalName is "Version"
                    or "AssemblyVersion"
                    or "FileVersion"
                    or "InformationalVersion"
                    or "VersionPrefix"
                    or "VersionSuffix");

            if (statesVersion)
            {
                offenders.Add(Path.GetRelativePath(repositoryRoot, project));
            }
        }

        declaredVersion.ShouldNotBeNullOrWhiteSpace(
            "Directory.Build.props must state the version the package is named after.");

        offenders.ShouldBeEmpty(
            "A project must not state a version of its own: the package is named after the one version "
            + $"the repository declares, so an assembly that carried another one would misname it. {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// Asks the publish script what it would run, without building anything.
    /// </summary>
    private static string RunPlan(string repositoryRoot)
    {
        foreach (string shell in (string[])["pwsh", "powershell"])
        {
            ProcessStartInfo startInfo = new(shell)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "scripts", "Publish-Release.ps1"));
            startInfo.ArgumentList.Add("-Plan");

            Process? process;

            try
            {
                process = Process.Start(startInfo);
            }
            catch (Win32Exception)
            {
                // This machine has the other shell; the plan does not care which one
                // reads it, so try the next.
                continue;
            }

            if (process is null)
            {
                continue;
            }

            using (process)
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                // The plan is read from wherever the test host happens to run, which
                // is the point of the script: it resolves the repository from its own
                // path rather than from a shell open in the folder.
                process.ExitCode.ShouldBe(0, $"The publish script could not state its plan: {error}");

                return output;
            }
        }

        throw new InvalidOperationException("Neither pwsh nor powershell is available to read the publish plan.");
    }

    private static bool IsBuildOutput(string file)
        => file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VisionWeave.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                $"The repository root was not found above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// What <c>Directory.Build.props</c> declares a package is.
    /// </summary>
    private sealed record Declared(
        string Version,
        string Project,
        string Configuration,
        string RuntimeIdentifier,
        bool SelfContained,
        string OutputRoot)
    {
        internal static Declared Read(string repositoryRoot)
        {
            XDocument props = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));

            return new Declared(
                Setting(props, "Version"),
                Setting(props, "VisionWeavePublishProject"),
                Setting(props, "VisionWeavePublishConfiguration"),
                Setting(props, "VisionWeavePublishRuntimeIdentifier"),
                bool.Parse(Setting(props, "VisionWeavePublishSelfContained")),
                Setting(props, "VisionWeavePublishOutputRoot"));
        }

        private static string Setting(XDocument props, string name)
            => props.Descendants(name).FirstOrDefault()?.Value.Trim()
                ?? throw new InvalidOperationException($"Directory.Build.props does not declare <{name}>.");
    }
}
