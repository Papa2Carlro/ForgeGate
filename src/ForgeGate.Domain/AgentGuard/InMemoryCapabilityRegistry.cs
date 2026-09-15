using System.Collections.Frozen;

namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// In-memory immutable Capability Registry.
/// 
/// This is the default implementation for Slice 19. It is:
/// - Immutable after construction
/// - Static (no runtime state changes)
/// - Thread-safe (read-only after initialization)
/// 
/// The registry contains a fixed set of canonical capabilities.
/// New capabilities must be added here explicitly.
/// 
/// Unknown capabilities return false from IsRegistered() and
/// throw from GetDescriptor() — they do NOT silently fallback
/// to any default capability.
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed class InMemoryCapabilityRegistry : ICapabilityRegistry
{
    private readonly FrozenDictionary<ActionIntentKind, CapabilityDescriptor> _descriptors;

    public InMemoryCapabilityRegistry()
    {
        _descriptors = new Dictionary<ActionIntentKind, CapabilityDescriptor>
        {
            { ActionIntentKind.FileRead, CapabilityDescriptor.FileRead() },
            { ActionIntentKind.FileWrite, CapabilityDescriptor.FileWrite() },
            { ActionIntentKind.DirectoryList, CapabilityDescriptor.DirectoryList() },
            { ActionIntentKind.FileSearch, CapabilityDescriptor.FileSearch() },
            { ActionIntentKind.FileDelete, CapabilityDescriptor.FileDelete() },
            { ActionIntentKind.HttpRequest, CapabilityDescriptor.HttpRequest() },
            { ActionIntentKind.ProcessSpawn, CapabilityDescriptor.ProcessSpawn() }
        }.ToFrozenDictionary();
    }

    public bool IsRegistered(ActionIntentKind capability) =>
        _descriptors.ContainsKey(capability);

    public CapabilityDescriptor GetDescriptor(ActionIntentKind capability) =>
        _descriptors.TryGetValue(capability, out var descriptor)
            ? descriptor
            : throw new KeyNotFoundException(
                $"Capability '{capability}' is not registered in the Capability Registry.");

    public IReadOnlyList<CapabilityDescriptor> ListAll() =>
        _descriptors.Values.ToList().AsReadOnly();
}
