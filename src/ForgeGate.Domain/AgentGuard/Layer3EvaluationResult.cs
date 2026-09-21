namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Result of Layer 3 risk/suspicion evaluation.
/// 
/// This type communicates the evaluation finding from Layer 3 to orchestration.
/// It semantically distinguishes whether a relevant risk/suspicion finding was present.
/// 
/// This is an implementation mechanism — the canonical semantic contract is open.
/// Exact representation may change without affecting the architectural boundary.
/// </summary>
public sealed class Layer3EvaluationResult
{
    private Layer3EvaluationResult(bool hasRiskFinding)
    {
        HasRiskFinding = hasRiskFinding;
    }

    /// <summary>
    /// Evaluation completed — a relevant risk/suspicion finding was present.
    /// </summary>
    public static Layer3EvaluationResult RiskFound => new(true);

    /// <summary>
    /// Evaluation completed — no relevant risk/suspicion finding was present.
    /// </summary>
    public static Layer3EvaluationResult NoRiskFound => new(false);

    /// <summary>
    /// Returns true if a relevant risk/suspicion finding was present.
    /// This is an implementation detail — the canonical contract is semantic only.
    /// </summary>
    public bool HasRiskFinding { get; }
}
