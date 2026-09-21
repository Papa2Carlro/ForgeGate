namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Contract for formatting Agent Guard structured events into user-visible notifications.
///
/// AlertFormatter transforms an AgentGuardEvent into a deterministic, reproducible,
/// and traceable text representation suitable for system alerts.
///
/// See: Docs/decisions/agent-guard-capability-translation.md §9
/// </summary>
public interface IAlertFormatter
{
    /// <summary>
    /// Formats an Agent Guard event into a user-visible alert string.
    /// </summary>
    /// <param name="event">The structured event to format.</param>
    /// <returns>A deterministic, human-readable alert string.</returns>
    string Format(AgentGuardEvent @event);
}
