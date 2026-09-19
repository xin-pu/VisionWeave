namespace VisionWeave.Contracts.Ports;

public readonly record struct PortTypeId
{
    public PortTypeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
