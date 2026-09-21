namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Outcome discriminator for Agent Guard structured events.
/// 
/// Distinguishes between successful pipeline completion and failures
/// at specific stages (normalization, translation, policy evaluation).
/// </summary>
public enum AgentGuardOutcomeType
{
    /// <summary>
    /// Pipeline completed successfully with a policy decision.
    /// </summary>
    PolicyEvaluated,

    /// <summary>
    /// Pipeline failed at normalization stage.
    /// </summary>
    NormalizationFailed,

    /// <summary>
    /// Pipeline failed at translation stage.
    /// </summary>
    TranslationFailed,

    /// <summary>
    /// Pipeline failed at policy evaluation stage.
    /// </summary>
    PolicyEvaluationFailed,
}
