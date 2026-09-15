using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Chat;

/// <summary>
/// Application-owned normalized provider failure.
/// Immutable fact describing provider execution failure without exposing infrastructure details.
/// </summary>
public sealed record ProviderFailure
{
    public ProviderFailureCategory Category { get; init; }
    public ProviderFailureRetryability Retryability { get; init; }
    public ProviderFailureScope Scope { get; init; }
    public int? UpstreamStatusCode { get; init; }
    public string? UpstreamCode { get; init; }
    public string? SanitizedUpstreamMessage { get; init; }
    public TimeSpan? RetryAfter { get; init; }
    public PolicyDecision? PolicyDecision { get; init; }
}
