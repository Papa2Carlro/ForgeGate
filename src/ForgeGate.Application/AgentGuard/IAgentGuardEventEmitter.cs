namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Contract for emitting Agent Guard structured events.
/// 
/// Implementations may persist, log, or forward events.
/// The NullAgentGuardEventEmitter provides a no-op default.
/// </summary>
public interface IAgentGuardEventEmitter
{
    /// <summary>
    /// Emits a structured event representing an Agent Guard pipeline outcome.
    /// </summary>
    void Emit(AgentGuardEvent @event);
}
