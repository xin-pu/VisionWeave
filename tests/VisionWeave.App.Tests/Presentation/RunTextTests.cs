using System.Globalization;
using Shouldly;
using VisionWeave.App.Presentation;
using VisionWeave.Application.Execution;

namespace VisionWeave.App.Tests.Presentation;

/// <summary>
/// Covers how the shell words a run: every terminal state a run can report has a
/// word, a time is shown only for the nodes the run measured one for, and the
/// number is written the same way wherever the shell runs.
/// </summary>
public sealed class RunTextTests
{
    [Fact]
    public void Every_state_a_run_can_report_has_a_word_that_reads_on_its_own()
    {
        foreach (NodeRunState state in Enum.GetValues<NodeRunState>())
        {
            RunText.Word(state).ShouldNotBeNullOrWhiteSpace();
        }

        // The state whose name is two words is the one a reader would otherwise see
        // as an enumeration member rather than as a sentence.
        RunText.Word(NodeRunState.NotRun).ShouldBe("Not run");
        RunText.Word(NodeRunState.Succeeded).ShouldBe("Succeeded");
        RunText.Word(NodeRunState.Failed).ShouldBe("Failed");
        RunText.Word(NodeRunState.Cancelled).ShouldBe("Cancelled");
        RunText.Word(NodeRunState.Blocked).ShouldBe("Blocked");
    }

    [Fact]
    public void A_node_the_run_measured_is_described_with_its_time()
    {
        RunText.Describe(NodeRunState.Succeeded, TimeSpan.FromMilliseconds(12.3)).ShouldBe("Succeeded in 12.3 ms");

        // A node that answered with a failure is a completed execution, so the run
        // measured it too.
        RunText.Describe(NodeRunState.Failed, TimeSpan.FromMilliseconds(3)).ShouldBe("Failed in 3 ms");

        // A long node reads in seconds rather than as four digits of milliseconds.
        RunText.Describe(NodeRunState.Succeeded, TimeSpan.FromSeconds(2.5)).ShouldBe("Succeeded in 2.50 s");
    }

    [Fact]
    public void A_node_the_run_never_measured_is_described_without_a_time()
    {
        // The run reports zero for work it never measured, so a duration would be a
        // number the user cannot read as anything but a very fast node.
        RunText.Describe(NodeRunState.NotRun, TimeSpan.Zero).ShouldBe("Not run");
        RunText.Describe(NodeRunState.Blocked, TimeSpan.Zero).ShouldBe("Blocked");
        RunText.Describe(NodeRunState.Cancelled, TimeSpan.Zero).ShouldBe("Cancelled");
    }

    [Fact]
    public void A_duration_reads_the_same_whatever_the_machine_writes_its_decimals_with()
    {
        // The decimal separator is the machine's by default, and a duration that
        // changed with it would read as a different number on a German desktop.
        // The current culture is the thread's, so setting it here leaves the tests
        // running beside this one alone.
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            RunText.Describe(NodeRunState.Succeeded, TimeSpan.FromMilliseconds(12.3))
                .ShouldBe("Succeeded in 12.3 ms");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
