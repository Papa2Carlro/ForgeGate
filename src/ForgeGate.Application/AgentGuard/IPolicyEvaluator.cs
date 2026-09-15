using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Contract for the policy evaluation stage of Agent Guard.
/// 
/// Responsibility: evaluate a successfully translated semantic capability
/// against policy rules and produce an explicit policy decision.
/// 
/// The evaluator MUST:
/// - Accept CapabilityTranslationResult (semantic data, NOT raw command text)
/// - Produce explicit Allow / Deny / RequireHumanApproval decisions
/// - Return explicit failure for unknown or unregistered capabilities
/// - NOT silently convert translation failures into Allow decisions
/// 
/// The evaluator MUST NOT:
/// - Execute the capability
/// - Inspect AgentAction.RawAction or any raw command text
/// - Parse commands or perform normalization
/// - Perform translation (that belongs to ICapabilityTranslator)
/// - Make risk/trust decisions outside the explicit policy model
/// 
/// This is a pure function contract — the evaluator must not:
/// - Mutate external state
/// - Make I/O calls
/// - Depend on provider-specific logic
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>
    /// Evaluates a successfully translated capability against policy rules.
    /// Returns PolicyEvaluationResult with explicit decision or failure.
    /// </summary>
    PolicyEvaluationResult Evaluate(CapabilityTranslationResult translation);
}
