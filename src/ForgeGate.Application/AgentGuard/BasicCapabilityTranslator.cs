using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Production-quality capability translator for Agent Guard.
/// 
/// This translator consumes semantic ActionIntent and validates it against
/// the canonical Capability Registry. It preserves all semantic fields
/// without modification, and fails safely for unknown or invalid intents.
/// 
/// Translation pipeline:
///   ActionIntent → Registry validation → CapabilityTranslationResult
/// 
/// Does NOT:
/// - Execute capabilities
/// - Make policy decisions
/// - Re-parse raw commands
/// - Add risk/trust/approval metadata
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed class BasicCapabilityTranslator : ICapabilityTranslator
{
    private readonly ICapabilityRegistry _registry;

    public BasicCapabilityTranslator(ICapabilityRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public CapabilityTranslationResult Translate(ActionIntent intent)
    {
        if (intent == null)
            throw new ArgumentNullException(nameof(intent));

        // Unknown capability cannot be translated to a concrete action
        if (intent.Capability == ActionIntentKind.Unknown)
        {
            return CapabilityTranslationResult.Failure(
                new TranslationFailure("Unknown capability cannot be translated"));
        }

        // Validate that the capability exists in the canonical registry
        if (!_registry.IsRegistered(intent.Capability))
        {
            return CapabilityTranslationResult.Failure(
                new TranslationFailure(
                    $"Capability '{intent.Capability}' is not registered in the Capability Registry"));
        }

        // Translation is a passthrough — preserve semantic fields exactly
        return CapabilityTranslationResult.Success(
            intent.Capability,
            intent.Target,
            intent.Metadata);
    }
}
