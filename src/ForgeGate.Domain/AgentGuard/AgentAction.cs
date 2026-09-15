namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Represents a raw agent action as observed at the point of origin.
/// This is an immutable fact record — it captures what the agent did,
/// not what was intended.
/// </summary>
public sealed record AgentAction
{
    public string Source { get; init; } = string.Empty;
    public string RawAction { get; init; } = string.Empty;
    public string? Payload { get; init; }
    public DateTime ObservedAt { get; init; } = DateTime.UtcNow;
}
