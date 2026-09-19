using Shouldly;
using VisionWeave.Contracts.Files;

namespace VisionWeave.IntegrationTests.Files;

/// <summary>
/// The rule that turns the path a document recorded into the file a node reads or
/// writes: what it resolves, and every declaration it refuses (ADR-0012). The rule
/// is pure text, so these tests need no file on disk.
/// </summary>
public sealed class WorkDirectoryPathTests
{
    private static readonly string WorkDirectory = Path.Combine(Path.GetTempPath(), "visionweave-work");

    [Theory]
    [InlineData("plate.png")]
    [InlineData("plates/plate.png")]
    [InlineData("plates/./plate.png")]
    [InlineData("plates/../plate.png")]
    public void TryResolve_resolves_a_relative_path_inside_the_working_directory(string declared)
    {
        WorkDirectoryPath.TryResolve(WorkDirectory, declared, out string resolved, out string? refusal).ShouldBeTrue();

        refusal.ShouldBeNull();
        resolved.ShouldBe(Path.GetFullPath(Path.Combine(WorkDirectory, declared)));
        Path.IsPathRooted(resolved).ShouldBeTrue();
    }

    [Fact]
    public void TryResolve_with_a_document_that_has_no_folder_refuses_and_says_to_save_it()
    {
        WorkDirectoryPath.TryResolve(null, "plate.png", out string resolved, out string? refusal).ShouldBeFalse();

        resolved.ShouldBeEmpty();
        refusal.ShouldNotBeNull().ShouldContain("Save");
    }

    [Fact]
    public void TryResolve_with_a_blank_declared_path_refuses()
    {
        WorkDirectoryPath.TryResolve(WorkDirectory, "   ", out string resolved, out string? refusal).ShouldBeFalse();

        resolved.ShouldBeEmpty();
        refusal.ShouldNotBeNull().ShouldContain("empty");
    }

    [Fact]
    public void TryResolve_an_absolute_path_refuses_because_a_workflow_names_its_files_relatively()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "plate.png");

        WorkDirectoryPath.TryResolve(WorkDirectory, absolute, out string resolved, out string? refusal).ShouldBeFalse();

        resolved.ShouldBeEmpty();
        refusal.ShouldNotBeNull().ShouldContain("absolute");
    }

    [Theory]
    [InlineData("../plate.png")]
    [InlineData("plates/../../plate.png")]
    [InlineData("plates/../../../etc/plate.png")]
    public void TryResolve_a_path_that_leaves_the_working_directory_refuses(string declared)
    {
        WorkDirectoryPath.TryResolve(WorkDirectory, declared, out string resolved, out string? refusal).ShouldBeFalse();

        resolved.ShouldBeEmpty();
        refusal.ShouldNotBeNull().ShouldContain("leaves the folder");
    }

    [Theory]
    [InlineData("plates/")]
    [InlineData("plates\\")]
    public void TryResolve_a_path_that_names_a_folder_refuses(string declared)
    {
        WorkDirectoryPath.TryResolve(WorkDirectory, declared, out string resolved, out string? refusal).ShouldBeFalse();

        resolved.ShouldBeEmpty();
        refusal.ShouldNotBeNull().ShouldContain("folder");
    }

    [Fact]
    public void TryResolve_a_name_with_a_character_a_file_name_cannot_hold_refuses()
    {
        // The character is taken from the platform rather than written down, so the
        // test asserts the rule instead of asserting that the tests run on Windows.
        string declared = $"pla{Path.GetInvalidFileNameChars()[0]}te.png";

        WorkDirectoryPath.TryResolve(WorkDirectory, declared, out string resolved, out string? refusal).ShouldBeFalse();

        resolved.ShouldBeEmpty();
        refusal.ShouldNotBeNull().ShouldContain("character");
    }
}
