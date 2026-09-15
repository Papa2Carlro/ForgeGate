using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Immutable fact record produced by Agent Guard after observing and
/// normalizing an agent action.
/// 
/// This is the structured runtime event that serves as the source of
/// truth for all downstream consumers (policy, audit, alerts).
/// It is NOT a UI notification — alerts are derived from this event.
/// </summary>
public sealed record AgentGuardEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public AgentActionObservation Observation { get; init; } = new();
    public ActionIntentResult NormalizationResult { get; init; } = ActionIntentResult.Unknown(string.Empty);
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
