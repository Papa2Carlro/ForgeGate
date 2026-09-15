namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Sealed result type for intent normalization outcomes.
/// 
/// Distinguishes between a successfully normalized intent and the
/// case where the normalizer cannot reliably determine the semantic
/// capability. The Unknown state must NOT be silently coerced into
/// an Allow or Block decision — that belongs to the Policy layer.
/// </summary>
public sealed class ActionIntentResult
{
    private ActionIntentResult() { }

    /// <summary>
    /// Normalization succeeded — the action was mapped to a known capability.
    /// </summary>
    public static ActionIntentResult Success(ActionIntent intent) =>
        new() { IsSuccess = true, Intent = intent, FailureReason = null };

    /// <summary>
    /// Normalization could not determine the semantic intent.
    /// The raw action is preserved for audit purposes.
    /// </summary>
    public static ActionIntentResult Unknown(string rawAction) =>
        new()
        {
            IsSuccess = false,
            Intent = null,
            FailureReason = new NormalizationFailure(rawAction)
        };

    public bool IsSuccess { get; init; }
    public ActionIntent? Intent { get; init; }
    public NormalizationFailure? FailureReason { get; init; }
}

/// <summary>
/// Carries the reason normalization failed, preserving the raw input for traceability.
/// </summary>
public sealed record NormalizationFailure(string RawAction);
