namespace VisionWeave.App.Preview;

/// <summary>
/// Shows the images a run published. It is a seam rather than a method on the view
/// model because a run executes off the thread the window's bindings belong to: the
/// object behind this interface decides where a preview may touch them, and the runtime
/// waits for the task it returns.
/// </summary>
internal interface IRunPreviewPresenter
{
    /// <summary>Shows every image one node published, in the order the node declared them.</summary>
    /// <param name="previews">The images of one node, which is never empty.</param>
    /// <returns>A task that completes once the shell holds the previews.</returns>
    Task PresentAsync(IReadOnlyList<RunPreview> previews);
}
