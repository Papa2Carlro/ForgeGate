using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Production-quality policy evaluator for Agent Guard.
/// 
/// This evaluator implements the baseline deterministic policy rules:
/// 
/// 1. Unknown/unregistered capability → PolicyEvaluationFailure (never Allow)
/// 2. Registered, non-destructive, non-external capability → Allow
/// 3. Registered destructive capability → RequireHumanApproval
/// 4. Registered external capability → RequireHumanApproval
/// 5. Registered both destructive AND external → RequireHumanApproval
/// 
/// These rules are descriptive, not heuristic:
/// - IsDestructive = true means the capability CAN irreversibly modify state
/// - IsExternal = true means the capability interacts with systems outside workspace
/// - Neither field automatically implies Deny
/// 
/// The evaluator consumes ONLY semantic data from ICapabilityRegistry.
/// It does NOT inspect AgentAction.RawAction or any raw command text.
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed class BasicPolicyEvaluator : IPolicyEvaluator
{
    private readonly ICapabilityRegistry _registry;

    public BasicPolicyEvaluator(ICapabilityRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public PolicyEvaluationResult Evaluate(CapabilityTranslationResult translation)
    {
        if (translation == null)
            throw new ArgumentNullException(nameof(translation));

        // Translation failure → Policy evaluation failure (never Allow)
        if (!translation.IsSuccess)
        {
            return PolicyEvaluationResult.Failure(
                new PolicyEvaluationFailure(
                    $"Translation failed: {translation.FailureReason?.Reason ?? "unknown error"}"));
        }

        // Validate that the capability is registered
        if (!_registry.IsRegistered(translation.Capability))
        {
            return PolicyEvaluationResult.Failure(
                new PolicyEvaluationFailure(
                    $"Capability '{translation.Capability}' is not registered in the Capability Registry"));
        }

        // Get descriptor to evaluate policy based on capability metadata
        var descriptor = _registry.GetDescriptor(translation.Capability);

        return EvaluateByDescriptor(descriptor);
    }

    /// <summary>
    /// Applies baseline deterministic policy rules based on capability metadata.
    /// </summary>
    private static PolicyEvaluationResult EvaluateByDescriptor(CapabilityDescriptor descriptor)
    {
        // Both destructive and external → RequireHumanApproval
        // (This is the safest precedence: destructive OR external)
        if (descriptor.IsDestructive || descriptor.IsExternal)
        {
            return PolicyEvaluationResult.Evaluate(
                PolicyDecision.RequireHumanApproval,
                $"Capability '{descriptor.Name}' requires human approval " +
                $"because it is {(descriptor.IsDestructive ? "destructive" : "external")}");
        }

        // Safe baseline: non-destructive, non-external → Allow
        return PolicyEvaluationResult.Evaluate(
            PolicyDecision.Allow,
            $"Capability '{descriptor.Name}' is allowed by baseline policy");
    }
}
