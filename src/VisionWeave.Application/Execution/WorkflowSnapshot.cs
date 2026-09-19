using VisionWeave.Domain.Workflows;

namespace VisionWeave.Application.Execution;

public sealed record WorkflowSnapshot(Guid DocumentId, long Revision)
{
    public static WorkflowSnapshot From(WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new WorkflowSnapshot(document.Id, document.Revision);
    }
}
