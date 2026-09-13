namespace ForgeGate.Domain.Providers;

public readonly record struct ModelRouteId
{
    public string Value { get; }

    private ModelRouteId(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ModelRouteId cannot be empty", nameof(value));
    }

    public static ModelRouteId From(string value) => new(value);
}
