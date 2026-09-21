using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Contract for Layer 3 risk/suspicion evaluation.
/// 
/// Layer 3 performs bounded risk/suspicion evaluation on candidate actions.
/// It communicates the evaluation finding (present/absent) to orchestration.
/// 
/// Layer 3 does NOT:
/// - Produce PolicyDecision
/// - Invoke Layer 4 (semantic reasoner)
/// - Produce REPLAN or ASK_USER
/// - Resolve contextual infrastructure
/// 
/// See: Docs/decisions/layer-3-evaluation-result-boundary.md
/// </summary>
public interface ILayer3Evaluator
{
    /// <summary>
    /// Evaluates a candidate action for risk/suspicion indicators.
    /// Returns an evaluation finding that semantically distinguishes
    /// whether a relevant risk/suspicion finding was present.
    /// </summary>
    /// <param name="capability">The translated capability being evaluated.</param>
    /// <param name="contextualEvidence">Contextual evidence supplied by orchestration (Option B).</param>
    /// <returns>Layer 3 evaluation result with present/absent semantic distinction.</returns>
    Layer3EvaluationResult Evaluate(ActionIntentKind capability, string? contextualEvidence = null);
}
