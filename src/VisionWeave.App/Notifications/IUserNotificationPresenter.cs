using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Notifications;

/// <summary>
/// The one place a user-facing message leaves the shell. It takes a diagnostic
/// rather than an exception so a caller cannot hand it a message built from
/// internal state: the diagnostic's message is already safe to show, and its
/// exception is for the log alone.
/// </summary>
internal interface IUserNotificationPresenter
{
    /// <summary>Shows one condition to the user.</summary>
    /// <param name="diagnostic">The condition to show.</param>
    void Present(NodeDiagnostic diagnostic);
}
