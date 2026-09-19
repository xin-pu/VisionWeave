using VisionWeave.Contracts.Diagnostics;

namespace VisionWeave.App.Commands;

/// <summary>
/// The outcome of running one command: how it finished and the conditions it
/// observed. The diagnostics travel even when a failure was already presented,
/// because a status area and a diagnostics panel show more than a message box
/// does.
/// </summary>
/// <param name="Completion">How the command finished.</param>
/// <param name="Diagnostics">The conditions the run produced, in the order they were reported.</param>
internal sealed record CommandExecutionResult(
    CommandCompletion Completion,
    IReadOnlyList<NodeDiagnostic> Diagnostics)
{
    /// <summary>Gets a value indicating whether the operation ran to completion.</summary>
    internal bool Succeeded => Completion == CommandCompletion.Completed;
}
