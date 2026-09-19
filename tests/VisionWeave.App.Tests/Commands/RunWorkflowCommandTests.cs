using Shouldly;
using VisionWeave.App.Composition;
using VisionWeave.App.Preview;
using VisionWeave.App.Tests.Support;
using VisionWeave.App.ViewModels;
using VisionWeave.Contracts.Diagnostics;
using VisionWeave.Contracts.Values;
using VisionWeave.OpenCv.Nodes;

namespace VisionWeave.App.Tests.Commands;

/// <summary>
/// Covers the shell's Run and Cancel actions: what a run refuses, what the status
/// area reports for each way a run can end, and how the two gestures share one
/// running state. The run itself — a real image read, transformed, written, and
/// previewed — is covered where it belongs, by the shell's own smoke test over a
/// real window and by the integration tests over the runner.
/// </summary>
public sealed class RunWorkflowCommandTests
{
    [Fact]
    public async Task Run_a_document_that_was_never_saved_is_refused_with_the_missing_document_path()
    {
        using RunHarness harness = new();
        harness.BuildImageWorkflow();

        await harness.Command.Command.ExecuteAsync(null);

        // The reason is the one a user can act on: a workflow that has never been
        // written has no folder for its file paths to resolve against, so every
        // file-backed node would fail for a reason the shell already knows.
        harness.Status.RunOutcome.ShouldBe("Not run: the workflow has not been saved yet");
        harness.Status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        harness.Status.Condition.ShouldContain(DiagnosticCodes.MissingDocumentPath);
        harness.Ledger.Created.ShouldBe(0);
        harness.Presented.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_a_document_that_fails_validation_is_refused_with_the_conditions_that_stop_it()
    {
        using RunHarness harness = new();
        harness.Add("visionweave.test.unknown");
        harness.SaveDocument();

        await harness.Command.Command.ExecuteAsync(null);

        harness.Status.RunOutcome.ShouldBe("Not run: the document reports 1 condition");
        harness.Status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        harness.Status.Condition.ShouldContain(DiagnosticCodes.MissingNodeDefinition);
        harness.Ledger.Created.ShouldBe(0);
    }

    [Fact]
    public async Task Run_a_workflow_whose_source_cannot_read_its_file_reports_a_failed_run()
    {
        using RunHarness harness = new();
        harness.BuildImageWorkflow(inputName: "missing.png");
        harness.SaveDocument();

        await harness.Command.Command.ExecuteAsync(null);

        // A failure the run itself reported stays a failure the user can read: the
        // node that could not read its file names it, and every frame the run held
        // was released on the way out.
        harness.Status.RunOutcome.ShouldBe(ShellStatus.RunFailedText);
        harness.Status.ConditionSeverity.ShouldBe(DiagnosticSeverity.Error);
        harness.Status.Condition.ShouldContain(DiagnosticCodes.NodeExecutionFailed);
        harness.Status.Condition.ShouldContain("missing.png");
        harness.Ledger.Outstanding.ShouldBe(0);
        harness.Ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_a_saved_workflow_writes_its_output_and_publishes_a_preview_per_image()
    {
        using RunHarness harness = new();
        harness.WriteImage("plate.png", width: 32, height: 24, value: 120);
        harness.BuildImageWorkflow();
        harness.SaveDocument();

        await harness.Command.Command.ExecuteAsync(null);

        harness.Status.RunOutcome.ShouldBe(ShellStatus.RanText);
        harness.Status.Condition.ShouldBe(ShellStatus.NoConditionText);
        System.IO.File.Exists(harness.PathOf("done.png")).ShouldBeTrue();

        // Both nodes that publish an image reached the preview surface, and what they
        // handed over is a frozen copy: the run converts frames on its own thread, and
        // a bitmap the shell may draw has to belong to no thread at all.
        IReadOnlyList<RunPreview> previews = harness.Presented;

        previews.Count.ShouldBe(2);
        previews[0].NodeTitle.ShouldBe("Image Source");
        previews[0].Width.ShouldBe(32);
        previews[^1].NodeTitle.ShouldBe("Resize");
        previews[^1].Width.ShouldBe(16);
        previews[^1].Height.ShouldBe(8);
        previews[^1].PixelFormat.ShouldBe(FramePixelFormat.Bgr24);
        previews.ShouldAllBe(preview => preview.Image.IsFrozen);

        harness.Ledger.Created.ShouldBe(2);
        harness.Ledger.Released.ShouldBe(2);
        harness.Ledger.Outstanding.ShouldBe(0);
        harness.Ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Run_stopped_while_a_node_is_still_running_reports_that_the_user_stopped_it()
    {
        // The run is held at the node that resizes, so the stop is made while a node
        // is executing rather than between two of them.
        using RunHarness harness = new(resolvers: ledger => new RunHold(new ExecutorRegistry(ledger)));
        harness.WriteImage("plate.png", width: 32, height: 24, value: 120);
        harness.BuildImageWorkflow();
        harness.SaveDocument();

        Task run = harness.Command.Command.ExecuteAsync(null);

        await harness.Executors.ShouldBeOfType<RunHold>().Started;

        // While the run is executing the two gestures are opposites: it cannot be
        // started again, and it can be stopped.
        harness.Command.Command.CanExecute(null).ShouldBeFalse();
        harness.Command.CancelCommand.CanExecute(null).ShouldBeTrue();

        harness.Command.CancelCommand.Execute(null);

        await run;

        harness.Status.RunOutcome.ShouldBe(ShellStatus.StoppedText);

        // A stop is the outcome of one gesture rather than a condition per node that
        // never started, so the shell reports nothing and the run wrote nothing.
        harness.Status.Condition.ShouldBe(ShellStatus.NoConditionText);
        System.IO.File.Exists(harness.PathOf("done.png")).ShouldBeFalse();
        harness.Command.CancelCommand.CanExecute(null).ShouldBeFalse();
        harness.Ledger.Outstanding.ShouldBe(0);
        harness.Ledger.ReservationsOutstanding.ShouldBe(0);
    }

    [Fact]
    public void Start_is_offered_until_the_first_run_and_then_offered_again()
    {
        using RunHarness harness = new();

        // Nothing about an unsaved document is disabled: running it is a gesture the
        // user can make, and the refusal is what explains why nothing ran.
        harness.Command.Command.CanExecute(null).ShouldBeTrue();
        harness.Command.CancelCommand.CanExecute(null).ShouldBeFalse();
    }
}
