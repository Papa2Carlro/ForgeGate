namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Canonical policy decision outcome produced by the Policy Evaluator.
/// 
/// These are the ONLY three autonomous policy outcomes. No boolean
/// IsAllowed — the distinction between Allow, Deny, and RequireHumanApproval
/// is semantically important for downstream handling.
/// 
/// - Allow: Policy explicitly permits autonomous execution.
/// - Deny: Policy explicitly rejects execution.
/// - RequireHumanApproval: Policy does not permit autonomous execution,
///   but the action may proceed after explicit human authorization.
/// 
/// Unknown or translation-failed capabilities produce a policy evaluation
/// failure, not an Allow decision.
/// </summary>
public enum PolicyDecision
{
    /// <summary>
    /// Policy explicitly permits autonomous execution.
    /// </summary>
    Allow,

    /// <summary>
    /// Policy explicitly rejects execution.
    /// </summary>
    Deny,

    /// <summary>
    /// Policy requires human authorization before execution can proceed.
    /// </summary>
    RequireHumanApproval,
}

/// <summary>
/// Immutable result of a policy evaluation attempt.
/// 
/// Distinguishes between:
/// - Decision: policy evaluated successfully and produced Allow/Deny/RequireHumanApproval
/// - Failure: evaluation could not proceed (null input, unknown capability,
///   translation failure passed through)
/// 
/// This result does NOT contain execution results — those belong to the Executor layer.
/// </summary>
public sealed class PolicyEvaluationResult
{
    private PolicyEvaluationResult() { }

    /// <summary>
    /// Policy evaluation succeeded — produced an explicit decision.
    /// </summary>
    public static PolicyEvaluationResult Evaluate(PolicyDecision decision, string? reason = null) =>
        new()
        {
            IsSuccess = true,
            Decision = decision,
            Reason = reason,
            FailureReason = null
        };

    /// <summary>
    /// Policy evaluation failed — could not produce a decision.
    /// This is NOT an Allow. Translation failures must flow through here.
    /// </summary>
    public static PolicyEvaluationResult Failure(PolicyEvaluationFailure failure) =>
        new()
        {
            IsSuccess = false,
            Decision = default,
            Reason = null,
            FailureReason = failure
        };

    public bool IsSuccess { get; init; }
    public PolicyDecision Decision { get; init; }
    public string? Reason { get; init; }
    public PolicyEvaluationFailure? FailureReason { get; init; }
}

/// <summary>
/// Carries the reason policy evaluation failed, for audit and debugging.
/// </summary>
public sealed record PolicyEvaluationFailure(string Reason);
