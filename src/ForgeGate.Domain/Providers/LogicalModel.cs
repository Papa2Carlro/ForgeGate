namespace ForgeGate.Domain.Providers;

public sealed record LogicalModel
{
    public required LogicalModelId Id { get; init; }
    public required string CanonicalName { get; init; }

    public LogicalModel()
    {
    }
}
