using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Application-level Agent Guard enforcement service.
/// 
/// This service coordinates the complete Agent Guard pipeline:
/// 
///     AgentAction → Normalization → Translation → Policy Evaluation → Result
/// 
/// It short-circuits on the first failure:
/// - Normalization failure → returns NormalizationFailed (translator and policy NOT called)
/// - Translation failure → returns TranslationFailed (policy NOT called)
/// - Policy evaluation failure → returns PolicyEvaluationFailed
/// 
/// The service does NOT:
/// - Execute capabilities
/// - Persist events
/// - Send alerts
/// - Integrate with the gateway
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed class AgentGuardService : IAgentGuard
{
    private readonly IActionIntentNormalizer _normalizer;
    private readonly ICapabilityTranslator _translator;
    private readonly IPolicyEvaluator _evaluator;
    private readonly ILayer3Evaluator? _layer3Evaluator;

    public AgentGuardService(
        IActionIntentNormalizer normalizer,
        ICapabilityTranslator translator,
        IPolicyEvaluator evaluator,
        ILayer3Evaluator? layer3Evaluator = null)
    {
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _layer3Evaluator = layer3Evaluator;
    }

    public AgentGuardResult Evaluate(AgentAction action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        // Stage 1: Normalize raw agent action to semantic intent
        var normalizeResult = _normalizer.Normalize(action);
        if (!normalizeResult.IsSuccess)
        {
            return AgentGuardResult.NormalizationFailed(
                normalizeResult.FailureReason!.RawAction,
                "Failed to normalize agent action to semantic intent");
        }

        var intent = normalizeResult.Intent!;

        // Stage 2: Translate semantic intent to canonical capability action
        var translateResult = _translator.Translate(intent);
        if (!translateResult.IsSuccess)
        {
            return AgentGuardResult.TranslationFailed(
                intent.Capability.ToString(),
                translateResult.FailureReason!.Reason);
        }

        // Stage 3: Evaluate translated capability against policy
        var policyResult = _evaluator.Evaluate(translateResult);
        if (!policyResult.IsSuccess)
        {
            return AgentGuardResult.PolicyEvaluationFailed(
                translateResult.Capability.ToString(),
                policyResult.FailureReason!.Reason);
        }

        // Stage 4: Layer 3 risk/suspicion evaluation (optional enrichment)
        // Layer 3 evaluates for risk indicators but does NOT produce PolicyDecision.
        // Orchestration interprets the finding and decides whether to escalate.
        Layer3EvaluationResult? layer3Result = null;
        if (_layer3Evaluator != null)
        {
            layer3Result = _layer3Evaluator.Evaluate(translateResult.Capability, translateResult.Metadata);
        }

        // Stage 5: Return successful enforcement outcome
        return AgentGuardResult.Success(
            policyResult.Decision,
            translateResult.Capability,
            translateResult.Target,
            translateResult.Metadata,
            policyResult.Reason,
            layer3Result);
    }
}
