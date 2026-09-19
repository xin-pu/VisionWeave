namespace VisionWeave.App.Commands;

/// <summary>
/// What the user answered when the shell asked a document question. Three answers
/// rather than two, because "no" and "stop" are different intentions: discarding
/// changes is a decision, and cancelling a question is the absence of one.
/// </summary>
internal enum WorkflowPromptAnswer
{
    /// <summary>The user agreed: save the changes, or recover the working copy.</summary>
    Accept,

    /// <summary>The user refused the first answer and chose the second one offered.</summary>
    Refuse,

    /// <summary>The user dismissed the question, so nothing follows from it.</summary>
    Cancel,
}

/// <summary>
/// One question the shell asks about the document, worded with the answers it
/// offers. The words are the caller's, because the same three answers mean
/// different things: "save or discard" and "recover or open the saved file".
/// </summary>
/// <param name="Question">The question itself.</param>
/// <param name="AcceptText">The label of the first answer.</param>
/// <param name="RefuseText">The label of the second answer.</param>
/// <param name="CancelText">The label that dismisses the question.</param>
internal sealed record WorkflowPrompt(
    string Question,
    string AcceptText,
    string RefuseText,
    string CancelText);

/// <summary>
/// The shell's one way of asking the user a question about the document. It is a
/// seam rather than a call to a dialog inside a view model, so a flow that depends
/// on an answer — saving before opening, recovering instead of opening — is
/// covered by a test that answers the question without a window.
/// </summary>
internal interface IWorkflowPrompt
{
    /// <summary>
    /// Asks the question and waits for an answer.
    /// </summary>
    /// <param name="prompt">The question and the answers it offers.</param>
    /// <param name="cancellationToken">The token that dismisses the question.</param>
    /// <returns>The answer, which is <see cref="WorkflowPromptAnswer.Cancel"/> when the question was dismissed.</returns>
    Task<WorkflowPromptAnswer> AskAsync(WorkflowPrompt prompt, CancellationToken cancellationToken = default);
}
