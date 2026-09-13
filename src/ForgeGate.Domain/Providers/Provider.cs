namespace ForgeGate.Domain.Providers;

public sealed record Provider
{
    public required ProviderId Id { get; init; }
    public required string Name { get; init; }

    public Provider()
    {
    }
}
