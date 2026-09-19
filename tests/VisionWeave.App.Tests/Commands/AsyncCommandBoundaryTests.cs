using Microsoft.Extensions.Logging;
using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.Tests.Support;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Tests.Commands;

public sealed class AsyncCommandBoundaryTests
{
    private readonly RecordingNotificationPresenter _presenter = new();
    private readonly RecordingLogger<AsyncCommandBoundary> _logger = new();

    [Fact]
    public async Task RunAsync_operation_reports_a_warning_returns_it_without_interrupting_the_user()
    {
        NodeDiagnostic warning = new(
            DiagnosticCodes.UnsupportedDocumentSchema,
            DiagnosticSeverity.Warning,
            "Schema version 2 is not supported by this build, so the document opens read-only.",
            null);

        CommandExecutionResult result = await Boundary().RunAsync(
            "OpenDocument",
            _ => Task.FromResult<IReadOnlyList<NodeDiagnostic>>([warning]),
            CancellationToken.None);

        result.Completion.ShouldBe(CommandCompletion.Completed);
        result.Diagnostics.ShouldBe([warning]);
        _presenter.Presented.ShouldBeEmpty();
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_operation_reports_a_failure_shows_it_without_logging_a_defect()
    {
        NodeDiagnostic failure = new(
            DiagnosticCodes.UnreadableDocument,
            DiagnosticSeverity.Error,
            "The workflow file could not be opened.",
            null);

        CommandExecutionResult result = await Boundary().RunAsync(
            "OpenDocument",
            _ => Task.FromResult<IReadOnlyList<NodeDiagnostic>>([failure]),
            CancellationToken.None);

        result.Completion.ShouldBe(CommandCompletion.Completed);
        result.Diagnostics.ShouldBe([failure]);
        _presenter.Presented.ShouldBe([failure]);
        _logger.Count(LogLevel.Error).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_unexpected_exception_is_logged_once_and_presented_as_a_safe_diagnostic()
    {
        InvalidOperationException defect = new(@"The document at C:\private\workflow.vwflow could not be parsed.");

        CommandExecutionResult result = await Boundary().RunAsync(
            "OpenDocument",
            _ => Task.FromException<IReadOnlyList<NodeDiagnostic>>(defect),
            CancellationToken.None);

        result.Completion.ShouldBe(CommandCompletion.Failed);

        NodeDiagnostic presented = _presenter.Presented.ShouldHaveSingleItem();
        presented.Code.ShouldBe(DiagnosticCodes.UnexpectedCommandFailure);
        presented.Severity.ShouldBe(DiagnosticSeverity.Error);
        presented.Message.ShouldNotContain("private");
        presented.Exception.ShouldBeSameAs(defect);

        _logger.Count(LogLevel.Error).ShouldBe(1);
        _logger.Entries.ShouldHaveSingleItem().Exception.ShouldBeSameAs(defect);
    }

    [Fact]
    public async Task RunAsync_requested_cancellation_is_reported_as_cancelled_without_an_error()
    {
        using CancellationTokenSource cancellation = new();

        CommandExecutionResult result = await Boundary().RunAsync(
            "OpenDocument",
            token =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<IReadOnlyList<NodeDiagnostic>>(token);
            },
            cancellation.Token);

        result.Completion.ShouldBe(CommandCompletion.Cancelled);
        result.Diagnostics.ShouldBeEmpty();
        _presenter.Presented.ShouldBeEmpty();
        _logger.Count(LogLevel.Error).ShouldBe(0);
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_cancellation_of_an_unrelated_token_is_treated_as_an_unexpected_failure()
    {
        using CancellationTokenSource unrelated = new();
        unrelated.Cancel();

        CommandExecutionResult result = await Boundary().RunAsync(
            "OpenDocument",
            _ => Task.FromCanceled<IReadOnlyList<NodeDiagnostic>>(unrelated.Token),
            CancellationToken.None);

        result.Completion.ShouldBe(CommandCompletion.Failed);
        _presenter.Presented.ShouldHaveSingleItem().Code.ShouldBe(DiagnosticCodes.UnexpectedCommandFailure);
        _logger.Count(LogLevel.Error).ShouldBe(1);
    }

    private AsyncCommandBoundary Boundary() => new(_presenter, _logger);
}
