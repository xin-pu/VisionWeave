using VisionWeave.Contracts.Values;

namespace VisionWeave.Application.Execution;

/// <summary>
/// The outcome of binding a node's inputs: either every required input has a
/// value, or the node cannot run and the reason explains which input is missing.
/// </summary>
internal sealed record NodeInputBindingResult(
    bool Succeeded,
    IReadOnlyDictionary<string, PortValue>? Values,
    string? Reason)
{
    internal static NodeInputBindingResult Bound(IReadOnlyDictionary<string, PortValue> values)
        => new(true, values, null);

    internal static NodeInputBindingResult Blocked(string reason)
        => new(false, null, reason);
}
