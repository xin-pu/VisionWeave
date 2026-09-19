using Shouldly;
using VisionWeave.App.Commands;
using VisionWeave.App.ViewModels;

namespace VisionWeave.App.Tests.ViewModels;

/// <summary>
/// The shell's prompt: the question it holds, the three answers it offers, and what
/// happens when the flow that asked is cancelled. It is both the seam a flow asks
/// through and the surface the question is drawn on, so a question that is asked is
/// always a question the user can see.
/// </summary>
public sealed class ShellPromptViewModelTests
{
    private static readonly WorkflowPrompt Question = new(
        "“workflow.vwflow” has unsaved changes. Save them before opening another document?",
        "Save",
        "Discard",
        "Cancel");

    [Fact]
    public async Task AskAsync_shows_the_question_and_the_three_answers_it_offers()
    {
        ShellPromptViewModel prompt = new();

        Task<WorkflowPromptAnswer> pending = prompt.AskAsync(Question);

        prompt.IsOpen.ShouldBeTrue();
        prompt.Question.ShouldBe(Question.Question);
        prompt.AcceptText.ShouldBe("Save");
        prompt.RefuseText.ShouldBe("Discard");
        prompt.CancelText.ShouldBe("Cancel");

        prompt.AcceptCommand.Execute(null);

        (await pending).ShouldBe(WorkflowPromptAnswer.Accept);
        prompt.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Accepting_the_question_returns_the_answer_the_user_chose()
    {
        ShellPromptViewModel prompt = new();
        Task<WorkflowPromptAnswer> pending = prompt.AskAsync(Question);

        prompt.AcceptCommand.Execute(null);

        (await pending).ShouldBe(WorkflowPromptAnswer.Accept);
        prompt.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Refusing_the_question_returns_the_answer_the_user_chose()
    {
        ShellPromptViewModel prompt = new();
        Task<WorkflowPromptAnswer> pending = prompt.AskAsync(Question);

        prompt.RefuseCommand.Execute(null);

        (await pending).ShouldBe(WorkflowPromptAnswer.Refuse);
        prompt.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Dismissing_the_question_returns_the_answer_the_user_chose()
    {
        ShellPromptViewModel prompt = new();
        Task<WorkflowPromptAnswer> pending = prompt.AskAsync(Question);

        prompt.DismissCommand.Execute(null);

        (await pending).ShouldBe(WorkflowPromptAnswer.Cancel);
        prompt.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task A_question_the_user_never_answers_stays_open()
    {
        ShellPromptViewModel prompt = new();

        Task<WorkflowPromptAnswer> pending = prompt.AskAsync(Question);

        // Nothing answers it, so the flow is still waiting: the shell shows the
        // question until a button is pressed, and the task never completes on its own.
        prompt.IsOpen.ShouldBeTrue();
        pending.IsCompleted.ShouldBeFalse();

        prompt.DismissCommand.Execute(null);
        (await pending).ShouldBe(WorkflowPromptAnswer.Cancel);
    }

    [Fact]
    public async Task A_cancelled_question_is_dismissed_and_decides_nothing()
    {
        ShellPromptViewModel prompt = new();
        using CancellationTokenSource cancellation = new();

        Task<WorkflowPromptAnswer> pending = prompt.AskAsync(Question, cancellation.Token);
        cancellation.Cancel();

        // Cancelling the flow and dismissing the question are the same answer: the
        // user decided nothing, so the flow stops rather than asking again.
        (await pending).ShouldBe(WorkflowPromptAnswer.Cancel);
        prompt.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancelling_after_the_answer_leaves_the_answer_that_was_given()
    {
        ShellPromptViewModel prompt = new();
        using CancellationTokenSource cancellation = new();

        Task<WorkflowPromptAnswer> pending = prompt.AskAsync(Question, cancellation.Token);
        prompt.AcceptCommand.Execute(null);
        cancellation.Cancel();

        (await pending).ShouldBe(WorkflowPromptAnswer.Accept);
    }

    [Fact]
    public async Task Asking_a_second_question_while_one_is_waiting_is_refused()
    {
        ShellPromptViewModel prompt = new();
        Task<WorkflowPromptAnswer> pending = prompt.AskAsync(Question);

        // A second question would replace the first while a flow is still waiting
        // for its answer, and which of the two the user is looking at would decide
        // what happens next.
        Should.Throw<InvalidOperationException>(() => { prompt.AskAsync(Question); })
            .Message.ShouldContain("one question at a time");

        prompt.RefuseCommand.Execute(null);
        (await pending).ShouldBe(WorkflowPromptAnswer.Refuse);

        // The surface is free again once the answer arrived.
        Task<WorkflowPromptAnswer> next = prompt.AskAsync(Question);
        prompt.AcceptCommand.Execute(null);
        (await next).ShouldBe(WorkflowPromptAnswer.Accept);
    }

    [Fact]
    public void A_question_nothing_asked_leaves_the_prompt_closed()
    {
        ShellPromptViewModel prompt = new();

        prompt.IsOpen.ShouldBeFalse();
        prompt.Question.ShouldBeEmpty();

        // An answer with no question behind it is nothing to act on, so it changes
        // nothing rather than reporting an error the user cannot cause.
        Should.NotThrow(() =>
        {
            prompt.AcceptCommand.Execute(null);
            prompt.RefuseCommand.Execute(null);
            prompt.DismissCommand.Execute(null);
        });
        prompt.IsOpen.ShouldBeFalse();
    }

    private static void Answer(ShellPromptViewModel prompt, WorkflowPromptAnswer answer)
    {
        switch (answer)
        {
            case WorkflowPromptAnswer.Accept:
                prompt.AcceptCommand.Execute(null);
                break;
            case WorkflowPromptAnswer.Refuse:
                prompt.RefuseCommand.Execute(null);
                break;
            default:
                prompt.DismissCommand.Execute(null);
                break;
        }
    }
}
