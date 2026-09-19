using System.Text.RegularExpressions;
using Shouldly;

namespace VisionWeave.ArchitectureTests.Conventions;

/// <summary>
/// Enforces the test-name form the design requires,
/// <c>Member_condition_expected_result</c>, by reading the test sources. No
/// analyzer covers a test method name, so the convention is checked here rather
/// than left to review.
/// </summary>
public sealed class TestNamingConventionTests
{
    private static readonly Regex TestMethod = new(
        @"\[(?:Fact|Theory)\][\s\S]{0,600}?\b(?:async\s+)?(?:Task|ValueTask|void)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    private const int RequiredSeparators = 2;

    [Fact]
    public void Test_method_names_separate_member_condition_and_expected_result()
    {
        string testsRoot = Path.Combine(RepositoryRoot(), "tests");
        List<string> offenders = [];
        int inspected = 0;

        foreach (string file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
            {
                continue;
            }

            foreach (Match match in TestMethod.Matches(File.ReadAllText(file)))
            {
                inspected++;
                string name = match.Groups["name"].Value;

                if (name.Count(character => character == '_') < RequiredSeparators)
                {
                    offenders.Add($"{Path.GetFileName(file)}: {name}");
                }
            }
        }

        inspected.ShouldBeGreaterThan(0, "No test method was found, so this check proved nothing.");

        offenders.ShouldBeEmpty(
            $"A test name must read Member_condition_expected_result: {Environment.NewLine}{string.Join(Environment.NewLine, offenders)}");
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
}
