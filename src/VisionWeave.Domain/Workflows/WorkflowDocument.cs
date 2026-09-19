namespace VisionWeave.Domain.Workflows;

public sealed class WorkflowDocument
{
    private WorkflowDocument(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string Name { get; }

    public long Revision { get; private set; }

    public static WorkflowDocument Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new WorkflowDocument(Guid.NewGuid(), name);
    }
}
