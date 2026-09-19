using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// Removes several node instances and the connections attached to them as one
/// edit. Deleting a selection is one gesture, so it is one undo unit: the command
/// reuses the single-node removal for its reversal data instead of repeating that
/// logic, and it refuses the whole edit when any named instance is absent, so a
/// selection that has gone stale cannot delete half of itself.
/// </summary>
public sealed class RemoveNodesCommand : IDocumentCommand
{
    private readonly Guid[] _instanceIds;
    private readonly List<RemoveNodeCommand> _removals = [];

    /// <summary>
    /// Initializes the command.
    /// </summary>
    /// <param name="instanceIds">The instances to remove.</param>
    /// <exception cref="ArgumentException">No instance was named.</exception>
    public RemoveNodesCommand(IEnumerable<Guid> instanceIds)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);

        _instanceIds = [.. instanceIds];
        if (_instanceIds.Length == 0)
        {
            throw new ArgumentException("A delete command needs at least one node.", nameof(instanceIds));
        }
    }

    /// <inheritdoc />
    public DocumentCommandResult Apply(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Every instance is resolved before the first removal, so an identifier the
        // document does not hold refuses the edit instead of deleting what came
        // before it in the selection.
        foreach (Guid instanceId in _instanceIds)
        {
            document.GetNode(instanceId);
        }

        _removals.Clear();
        foreach (Guid instanceId in _instanceIds)
        {
            var removal = new RemoveNodeCommand(instanceId);
            DocumentCommandResult result = removal.Apply(document);
            if (!result.IsAccepted)
            {
                return result;
            }

            _removals.Add(removal);
        }

        return DocumentCommandResult.Accepted;
    }

    /// <inheritdoc />
    public void Revert(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_removals.Count == 0)
        {
            throw new InvalidOperationException("The command cannot be reverted before it has been applied.");
        }

        // Reversed, mirroring the order the removals were applied in: a connection
        // between two removed nodes was taken away by the first removal and is
        // restored by its reversal, so the node it refers to has to exist again.
        for (int index = _removals.Count - 1; index >= 0; index--)
        {
            _removals[index].Revert(document);
        }
    }
}
