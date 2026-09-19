using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Editing;

/// <summary>
/// Sets a parameter value on a node instance. The undo data is the value the
/// parameter held before the first edit of the current undo unit, which is what
/// lets successive edits of one parameter share a single undo step: the history
/// keeps the earliest command of the unit, that command restores the value it
/// captured, and it takes over the value of every edit that joins the unit so it can
/// also restore where the user ended.
/// </summary>
public sealed class SetNodeParameterCommand : IDocumentCommand, ICoalescingCommand
{
    private readonly Guid _instanceId;
    private readonly string _parameterName;
    private object? _value;
    private bool _captured;
    private bool _hadValue;
    private object? _previousValue;

    /// <summary>
    /// Initializes the command.
    /// </summary>
    /// <param name="instanceId">The instance to edit.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="value">The new value.</param>
    public SetNodeParameterCommand(Guid instanceId, string parameterName, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);

        _instanceId = instanceId;
        _parameterName = parameterName;
        _value = value;
    }

    /// <inheritdoc />
    public DocumentCommandResult Apply(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        NodeInstance node = document.GetNode(_instanceId);

        if (!_captured)
        {
            _hadValue = node.Parameters.TryGetValue(_parameterName, out _previousValue);
            _captured = true;
        }

        document.SetNodeParameter(_instanceId, _parameterName, _value);
        return DocumentCommandResult.Accepted;
    }

    /// <inheritdoc />
    public void Revert(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (_hadValue)
        {
            document.SetNodeParameter(_instanceId, _parameterName, _previousValue);
            return;
        }

        // The parameter was unset before the edit, so unsetting it again is the
        // exact reversal: the node falls back to the definition's default.
        document.ClearNodeParameter(_instanceId, _parameterName);
    }

    /// <inheritdoc />
    bool ICoalescingCommand.ContinuesInto(IDocumentCommand next)
        => next is SetNodeParameterCommand other
            && other._instanceId == _instanceId
            && string.Equals(other._parameterName, _parameterName, StringComparison.Ordinal);

    /// <inheritdoc />
    void ICoalescingCommand.ContinueWith(IDocumentCommand next)
    {
        // The edit that joined this unit has already been applied, so the document
        // holds its value while the history keeps this command. Taking the value over
        // is what lets this command redo the unit: without it, redoing a merged unit
        // would write the value its first edit set rather than the one the user
        // settled on.
        if (next is SetNodeParameterCommand other
            && other._instanceId == _instanceId
            && string.Equals(other._parameterName, _parameterName, StringComparison.Ordinal))
        {
            _value = other._value;
        }
    }
}
