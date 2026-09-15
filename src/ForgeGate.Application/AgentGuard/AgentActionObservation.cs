using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Immutable snapshot of an observed agent action at a point in time.
/// 
/// Observation is a passive act — it records that an action occurred,
/// without yet interpreting its semantic intent or evaluating policy.
/// Separation of concerns: Observation ≠ Normalization ≠ Policy ≠ Execution.
/// </summary>
public sealed record AgentActionObservation
{
    public AgentAction Source { get; init; } = new();
    public DateTime ObservedAt { get; init; } = DateTime.UtcNow;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
