namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// No-op event emitter — discards all events.
/// 
/// Used as the default when no event infrastructure is configured.
/// Allows Agent Guard to operate without event emission dependencies.
/// </summary>
public sealed class NullAgentGuardEventEmitter : IAgentGuardEventEmitter
{
    public static readonly NullAgentGuardEventEmitter Instance = new();

    private NullAgentGuardEventEmitter() { }

    public void Emit(AgentGuardEvent @event)
    {
        // No-op: events are discarded
    }
}
