using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionWeave.App.Commands;

namespace VisionWeave.App.ViewModels;

/// <summary>
/// The shell's prompt: the question the shell is waiting on, drawn over the work
/// surface, and the three answers it offers. It is both the seam a flow asks
/// through and the surface that shows the question, so a flow that needs an answer
/// cannot be left waiting on something the shell never presents, and the shell
/// never opens a second window or a message box to ask.
/// </summary>
internal sealed partial class ShellPromptViewModel : ObservableObject, IWorkflowPrompt
{
    private TaskCompletionSource<WorkflowPromptAnswer>? _pending;

    /// <summary>
    /// Gets or sets a value indicating whether a question is being asked, which is
    /// what the shell draws the question for.
    /// </summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Gets the question being asked.</summary>
    [ObservableProperty]
    public partial string Question { get; private set; } = string.Empty;

    /// <summary>Gets the label of the answer that continues the flow.</summary>
    [ObservableProperty]
    public partial string AcceptText { get; private set; } = string.Empty;

    /// <summary>Gets the label of the answer that continues the flow without the change.</summary>
    [ObservableProperty]
    public partial string RefuseText { get; private set; } = string.Empty;

    /// <summary>Gets the label of the answer that stops the flow.</summary>
    [ObservableProperty]
    public partial string CancelText { get; private set; } = string.Empty;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">A question is already being asked.</exception>
    public Task<WorkflowPromptAnswer> AskAsync(
        WorkflowPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        if (_pending is not null)
        {
            // The shell asks one question at a time: a second one would replace the
            // first while a flow is still waiting for its answer, and which of the
            // two the user is looking at would decide what happens next.
            throw new InvalidOperationException("The shell asks one question at a time.");
        }

        Question = prompt.Question;
        AcceptText = prompt.AcceptText;
        RefuseText = prompt.RefuseText;
        CancelText = prompt.CancelText;

        // The answer is completed on the thread that answered it, which is the
        // dispatcher: the flow waiting here continues on the thread that owns the
        // shell, so the session it changes afterwards is still changed on it.
        var answer = new TaskCompletionSource<WorkflowPromptAnswer>();

        _pending = answer;
        IsOpen = true;

        // A dismissed question and a cancelled one are the same thing to the flow:
        // neither of them decided anything, so the flow stops rather than repeating
        // the question it was in the middle of.
        return cancellationToken.CanBeCanceled
            ? AwaitAnswerAsync(answer, cancellationToken)
            : answer.Task;
    }

    /// <summary>Answers the question with the change the flow offered first.</summary>
    [RelayCommand]
    private void Accept() => Answer(WorkflowPromptAnswer.Accept);

    /// <summary>Answers the question with the other answer it offered.</summary>
    [RelayCommand]
    private void Refuse() => Answer(WorkflowPromptAnswer.Refuse);

    /// <summary>Dismisses the question, which decides nothing.</summary>
    [RelayCommand]
    private void Dismiss() => Answer(WorkflowPromptAnswer.Cancel);

    /// <summary>
    /// Completes the pending question. The answer arrives from the dispatcher the
    /// question was asked on, so the flow that is waiting for it continues on the
    /// thread that owns the shell rather than on a worker.
    /// </summary>
    private void Answer(WorkflowPromptAnswer answer)
    {
        TaskCompletionSource<WorkflowPromptAnswer>? pending = _pending;
        if (pending is null)
        {
            return;
        }

        _pending = null;
        IsOpen = false;
        pending.TrySetResult(answer);
    }

    private async Task<WorkflowPromptAnswer> AwaitAnswerAsync(
        TaskCompletionSource<WorkflowPromptAnswer> answer,
        CancellationToken cancellationToken)
    {
        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => Dismiss(answer));

        return await answer.Task.ConfigureAwait(true);
    }

    /// <summary>
    /// Dismisses one question when the token that asked it is cancelled. The
    /// question is named explicitly, so a cancellation that arrives after the user
    /// already answered completes nothing.
    /// </summary>
    private void Dismiss(TaskCompletionSource<WorkflowPromptAnswer> answer)
    {
        if (ReferenceEquals(_pending, answer))
        {
            Answer(WorkflowPromptAnswer.Cancel);
        }
    }
}
