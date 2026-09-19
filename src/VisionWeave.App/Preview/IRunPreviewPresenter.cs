namespace VisionWeave.App.Preview;

/// <summary>
/// Shows the preview a run published. It is a seam rather than a method on the view
/// model because a run executes off the thread the window's bindings belong to: the
/// object behind this interface decides where a preview may touch them, and the runtime
/// waits for the task it returns.
/// </summary>
internal interface IRunPreviewPresenter
{
    /// <returns>A task that completes once the shell holds the preview.</returns>
    Task PresentAsync(RunPreview preview);
}
