using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Contract for the capability translation stage of Agent Guard.
/// 
/// Responsibility: translate a canonical semantic Intent (ActionIntent) into
/// a structured capability action representation (CapabilityTranslationResult)
/// that downstream layers (Policy, Executor) can consume.
/// 
/// The translator MUST:
/// - Accept ActionIntent (semantic intent, NOT raw command text)
/// - Validate capability identity against the canonical registry
/// - Preserve semantic fields (Target, Metadata)
/// - Return explicit failure for unknown/unregistered capabilities
/// 
/// The translator MUST NOT:
/// - Execute the capability
/// - Make policy decisions (allow/deny)
/// - Add risk scores, trust levels, or approval requirements
/// - Inspect AgentAction.RawAction or any raw command text
/// - Substitute one capability for another
/// 
/// This is a pure function contract — the translator must not:
/// - Mutate external state
/// - Make I/O calls
/// - Depend on provider-specific logic
/// </summary>
public interface ICapabilityTranslator
{
    /// <summary>
    /// Translates a semantic ActionIntent into a structured capability action.
    /// Returns Success with preserved semantic fields, or Failure if the
    /// capability is unknown or invalid.
    /// </summary>
    CapabilityTranslationResult Translate(ActionIntent intent);
}
