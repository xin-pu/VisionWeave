namespace VisionWeave.Application.Execution;

/// <summary>
/// One batch of the plan whose nodes can run in parallel once the previous level
/// has finished. The runtime still limits how many of them start at once.
/// </summary>
/// <param name="Index">The position of the level in the plan, starting at zero.</param>
/// <param name="NodeInstanceIds">The nodes of the level, in deterministic order.</param>
public sealed record ExecutionLevel(int Index, IReadOnlyList<Guid> NodeInstanceIds);
