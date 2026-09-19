using CommunityToolkit.Mvvm.ComponentModel;
using VisionWeave.App.Commands;
using VisionWeave.App.Presentation;
using VisionWeave.App.Sessions;
using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.ViewModels;

/// <summary>
/// The durable state the status area reports: what the document is, what the shell
/// is doing, what the last run produced, and the newest condition the shell saw.
/// It is the counterpart of the transient snackbar — a condition that is worth
/// keeping in front of the user stays here, and one that is only worth announcing
/// goes to the presenter — and it derives every string from the session and the
/// results a command reports rather than holding a second copy of either.
/// </summary>
internal sealed partial class ShellStatus : ObservableObject
{
    /// <summary>The state reported while the shell is waiting for a gesture.</summary>
    internal const string ReadyText = "Ready";

    /// <summary>
    /// The state reported after the user stopped an operation. Cancellation is an
    /// outcome and not a failure, so it is named plainly and records no condition.
    /// </summary>
    internal const string StoppedText = "Stopped at your request";

    /// <summary>The run-outcome field before anything can run.</summary>
    internal const string NoRunText = "No run yet";

    /// <summary>The condition field before the shell has observed anything.</summary>
    internal const string NoConditionText = "No conditions reported yet";

    private readonly EditorSession _session;

    /// <summary>
    /// Creates the status area over the session it reports on.
    /// </summary>
    /// <param name="session">The session the document state is read from.</param>
    /// <remarks>
    /// The constructor is public because the host's service container resolves this
    /// type and only considers public constructors; the type itself stays internal
    /// to the application.
    /// </remarks>
    public ShellStatus(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;

        BackgroundOperation = ReadyText;
        RunOutcome = NoRunText;
        Condition = NoConditionText;

        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <summary>
    /// Gets what the shell is doing, or what it last did: the operation while it
    /// runs, or the state it returned to when it finished.
    /// </summary>
    /// <remarks>
    /// The properties below are public because the status area is bound to them and
    /// WPF binds only to public members: a non-public one fails silently, leaving an
    /// empty field rather than an error. The type itself stays internal.
    /// </remarks>
    [ObservableProperty]
    public partial string BackgroundOperation { get; private set; }

    /// <summary>Gets the outcome of the last workflow run, which nothing produces yet.</summary>
    [ObservableProperty]
    public partial string RunOutcome { get; private set; }

    /// <summary>
    /// Gets the newest condition the shell observed, named by its severity word,
    /// its stable code, and its safe message, so the state is readable without
    /// relying on colour.
    /// </summary>
    [ObservableProperty]
    public partial string Condition { get; private set; }

    /// <summary>
    /// Gets how serious that condition is, which the status area also uses to pick
    /// a colour. It carries no meaning on its own: the wording says the same thing.
    /// </summary>
    [ObservableProperty]
    public partial DiagnosticSeverity? ConditionSeverity { get; private set; }

    /// <summary>
    /// Gets the state of the document being edited: whether it has ever been saved
    /// and whether it holds changes its file does not.
    /// </summary>
    public string DocumentState
        => _session.Path is null
            ? "Not saved yet"
            : _session.IsDirty ? "Unsaved changes" : "Saved";

    /// <summary>
    /// Reports that an operation started, so the status area names what the shell
    /// is waiting for instead of showing the previous outcome.
    /// </summary>
    /// <param name="activity">The phrase describing the operation, such as "Opening a document…".</param>
    internal void Begin(string activity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activity);

        BackgroundOperation = activity;
    }

    /// <summary>
    /// Reports how an operation finished.
    /// </summary>
    /// <param name="result">The outcome the command boundary returned.</param>
    internal void Report(CommandExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Completion == CommandCompletion.Cancelled)
        {
            // The user stopped the operation, so nothing was observed. The status
            // says the shell stopped and clears the condition, exactly as an
            // operation that observed nothing does: an old condition left standing
            // would read as the outcome of the operation that was just stopped.
            BackgroundOperation = StoppedText;
            ClearCondition();
            return;
        }

        BackgroundOperation = ReadyText;

        // The condition field describes the shell's current state rather than a
        // history of every message, so an operation that observed nothing clears
        // it. The record of what happened belongs to the log and, later, to the
        // diagnostics panel.
        if (result.Diagnostics.Count == 0)
        {
            ClearCondition();
            return;
        }

        NodeDiagnostic diagnostic = result.Diagnostics[0];
        ConditionSeverity = diagnostic.Severity;
        Condition = $"{SeverityText.Of(diagnostic.Severity)}: {diagnostic.Code} {diagnostic.Message}";
    }

    private void ClearCondition()
    {
        ConditionSeverity = null;
        Condition = NoConditionText;
    }

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => OnPropertyChanged(nameof(DocumentState));
}
