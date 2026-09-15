namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Final enforcement outcome of the complete Agent Guard pipeline.
/// 
/// This result preserves the full semantic context from normalization
/// through policy evaluation, so downstream consumers (executor, audit,
/// alerts) can make informed decisions.
/// 
/// The result distinguishes between:
/// - Successful pipeline with explicit policy decision (Allow/Deny/RequireHumanApproval)
/// - Failures at any stage (normalization, translation, policy evaluation)
/// 
/// A Failure state is NOT an Allow — it means the pipeline could not
/// produce a definitive policy decision.
/// </summary>
public sealed class AgentGuardResult
{
    private AgentGuardResult() { }

    /// <summary>
    /// Pipeline succeeded — policy produced an explicit decision.
    /// </summary>
    public static AgentGuardResult Success(
        PolicyDecision decision,
        ActionIntentKind capability,
        string target,
        string? metadata = null,
        string? reason = null) =>
        new()
        {
            IsSuccess = true,
            Decision = decision,
            Capability = capability,
            Target = target,
            Metadata = metadata,
            Reason = reason,
            FailureStage = null,
            FailureReason = null
        };

    /// <summary>
    /// Pipeline failed at the normalization stage.
    /// </summary>
    public static AgentGuardResult NormalizationFailed(string rawAction, string reason) =>
        new()
        {
            IsSuccess = false,
            Decision = default,
            Capability = default,
            Target = string.Empty,
            Metadata = null,
            Reason = reason,
            FailureStage = "Normalization",
            FailureReason = rawAction
        };

    /// <summary>
    /// Pipeline failed at the translation stage.
    /// </summary>
    public static AgentGuardResult TranslationFailed(string capability, string reason) =>
        new()
        {
            IsSuccess = false,
            Decision = default,
            Capability = default,
            Target = string.Empty,
            Metadata = null,
            Reason = reason,
            FailureStage = "Translation",
            FailureReason = capability
        };

    /// <summary>
    /// Pipeline failed at the policy evaluation stage.
    /// </summary>
    public static AgentGuardResult PolicyEvaluationFailed(string capability, string reason) =>
        new()
        {
            IsSuccess = false,
            Decision = default,
            Capability = default,
            Target = string.Empty,
            Metadata = null,
            Reason = reason,
            FailureStage = "PolicyEvaluation",
            FailureReason = capability
        };

    public bool IsSuccess { get; init; }
    public PolicyDecision Decision { get; init; }
    public ActionIntentKind Capability { get; init; }
    public string Target { get; init; } = string.Empty;
    public string? Metadata { get; init; }
    public string? Reason { get; init; }
    public string? FailureStage { get; init; }
    public string? FailureReason { get; init; }
}
