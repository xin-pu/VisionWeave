using System.Globalization;
using VisionWeave.Application.Execution;

namespace VisionWeave.App.Presentation;

/// <summary>
/// Names what a run did to a node. A node states the outcome in words the way it
/// states a condition's severity, so the mark reads without relying on its colour,
/// and the elapsed time is written once for every surface that shows one.
/// </summary>
internal static class RunText
{
    /// <summary>
    /// Names a node's terminal state in words.
    /// </summary>
    /// <param name="state">The state to name.</param>
    /// <returns>The word the shell shows for that state.</returns>
    internal static string Word(NodeRunState state)
        => state switch
        {
            NodeRunState.Succeeded => "Succeeded",
            NodeRunState.Failed => "Failed",
            NodeRunState.Cancelled => "Cancelled",
            NodeRunState.Blocked => "Blocked",
            _ => "Not run",
        };

    /// <summary>
    /// Describes what a run did to a node. Only a node that ran carries a time: a
    /// node that was blocked, never started, or stopped reports the zero the run
    /// leaves for work it never measured, so naming a duration there would be
    /// reporting a measurement the run did not make.
    /// </summary>
    /// <param name="state">The state to name.</param>
    /// <param name="duration">How long the node was executing.</param>
    /// <returns>The description, such as <c>Succeeded in 12.3 ms</c>.</returns>
    internal static string Describe(NodeRunState state, TimeSpan duration)
        => state is NodeRunState.Succeeded or NodeRunState.Failed
            ? $"{Word(state)} in {Duration(duration)}"
            : Word(state);

    /// <summary>
    /// Writes an elapsed time the way the readout shows it. The number is written
    /// in the invariant culture, so a duration reads the same wherever the shell
    /// runs rather than changing its separator with the machine's locale.
    /// </summary>
    private static string Duration(TimeSpan elapsed)
        => elapsed.TotalSeconds >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalSeconds:0.00} s")
            : string.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalMilliseconds:0.#} ms");
}
