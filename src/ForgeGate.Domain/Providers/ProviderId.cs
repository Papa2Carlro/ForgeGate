namespace ForgeGate.Domain.Providers;

public readonly record struct ProviderId
{
    public string Value { get; }

    private ProviderId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProviderId cannot be empty", nameof(value));
    }

    public static ProviderId From(string value) => new(value);
}
