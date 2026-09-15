namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Interface for the Capability Registry — the canonical source of semantic
/// capability identities and descriptors.
/// 
/// The registry:
/// - Defines what capabilities exist
/// - Provides immutable descriptors for each capability
/// - Does NOT execute capabilities
/// - Does NOT make policy decisions
/// - Does NOT translate raw commands
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public interface ICapabilityRegistry
{
    /// <summary>
    /// Returns true if the given capability is registered.
    /// </summary>
    bool IsRegistered(ActionIntentKind capability);

    /// <summary>
    /// Returns the immutable descriptor for the given capability.
    /// Throws KeyNotFoundException if the capability is not registered.
    /// </summary>
    CapabilityDescriptor GetDescriptor(ActionIntentKind capability);

    /// <summary>
    /// Returns all registered capabilities with their descriptors.
    /// </summary>
    IReadOnlyList<CapabilityDescriptor> ListAll();
}
