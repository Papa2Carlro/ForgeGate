namespace ForgeGate.Domain.Providers;

public readonly record struct LogicalModelId
{
    public string Value { get; }

    private LogicalModelId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("LogicalModelId cannot be empty", nameof(value));
    }

    public static LogicalModelId From(string value) => new(value);
}
