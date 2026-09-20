using VisionWeave.Application.Execution;

namespace VisionWeave.App.Canvas;

/// <summary>
/// What the newest run did to one node: how it ended, how long the run measured it
/// for, and the condition the run reported for it. The projection builds it from
/// the run's own report, and a node the run did not cover has none.
/// </summary>
/// <param name="State">How the node ended.</param>
/// <param name="Duration">How long the node was executing.</param>
/// <param name="Condition">The condition the run reported for the node, or an empty string.</param>
internal sealed record NodeRunMark(NodeRunState State, TimeSpan Duration, string Condition);
