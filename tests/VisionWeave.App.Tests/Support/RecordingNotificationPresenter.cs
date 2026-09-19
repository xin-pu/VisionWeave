using VisionWeave.App.Notifications;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Tests.Support;

/// <summary>
/// Keeps every condition a presenter was asked to show, so a test asserts what
/// the user would have seen instead of opening a window.
/// </summary>
internal sealed class RecordingNotificationPresenter : IUserNotificationPresenter
{
    private readonly List<NodeDiagnostic> _presented = [];

    /// <summary>Gets the conditions that were presented, in order.</summary>
    internal IReadOnlyList<NodeDiagnostic> Presented => _presented;

    /// <inheritdoc />
    public void Present(NodeDiagnostic diagnostic) => _presented.Add(diagnostic);
}
