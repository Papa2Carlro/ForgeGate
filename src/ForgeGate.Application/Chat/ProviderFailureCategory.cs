namespace ForgeGate.Application.Chat;

/// <summary>
/// Normalized failure category for provider execution.
/// Describes what went wrong, independent of retryability or scope.
/// </summary>
public enum ProviderFailureCategory
{
    AuthenticationFailed,
    AuthorizationFailed,
    RateLimited,
    QuotaExhausted,
    ProviderUnavailable,
    Timeout,
    NetworkFailure,
    InvalidModel,
    ContextExceeded,
    InvalidRequest,
    MalformedResponse,
    UnknownProviderFailure,
    ConcurrencyLimited,
    AgentGuardDenied
}
