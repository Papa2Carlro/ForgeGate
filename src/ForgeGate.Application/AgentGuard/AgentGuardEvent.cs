using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Immutable structured event representing one Agent Guard pipeline intervention.
///
/// Captures the complete outcome of evaluating a single AgentAction through:
/// - Observation (raw action)
/// - Normalization (semantic intent extraction)
/// - Translation (capability mapping)
/// - Policy evaluation (Allow/Deny/RequireHumanApproval)
/// - Layer 3 risk/suspicion evaluation
///
/// Event is produced on every Evaluate() call — success or failure.
/// It serves as the source of truth for audit, alerts, and supervision.
/// </summary>
public sealed record AgentGuardEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public AgentActionObservation Observation { get; init; } = new();
    public AgentGuardOutcomeType OutcomeType { get; init; }
    public PolicyDecision? Decision { get; init; }
    public ActionIntentKind? Capability { get; init; }
    public string? Target { get; init; }
    public string? Metadata { get; init; }
    public Layer3EvaluationResult? Layer3Result { get; init; }
    public string? FailureStage { get; init; }
    public string? FailureReason { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful outcome event from AgentGuardResult.
    /// </summary>
    public static AgentGuardEvent FromSuccess(AgentGuardResult result, AgentActionObservation observation) =>
        new()
        {
            Observation = observation,
            OutcomeType = AgentGuardOutcomeType.PolicyEvaluated,
            Decision = result.Decision,
            Capability = result.Capability,
            Target = result.Target,
            Metadata = result.Metadata,
            Layer3Result = result.Layer3Result,
            FailureStage = null,
            FailureReason = null,
            CreatedAt = DateTime.UtcNow
        };

    /// <summary>
    /// Creates a failure outcome event from AgentGuardResult.
    /// </summary>
    public static AgentGuardEvent FromFailure(AgentGuardResult result, AgentActionObservation observation) =>
        new()
        {
            Observation = observation,
            OutcomeType = result.FailureStage switch
            {
                "Normalization" => AgentGuardOutcomeType.NormalizationFailed,
                "Translation" => AgentGuardOutcomeType.TranslationFailed,
                "PolicyEvaluation" => AgentGuardOutcomeType.PolicyEvaluationFailed,
                _ => AgentGuardOutcomeType.PolicyEvaluationFailed
            },
            Decision = null,
            Capability = result.Capability,
            Target = result.Target,
            Metadata = null,
            Layer3Result = null,
            FailureStage = result.FailureStage,
            FailureReason = result.FailureReason,
            CreatedAt = DateTime.UtcNow
        };
}
