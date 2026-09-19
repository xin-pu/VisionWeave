namespace VisionWeave.Contracts.Nodes;

public readonly record struct NodeTypeId
{
    public NodeTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
