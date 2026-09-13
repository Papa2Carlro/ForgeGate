namespace ForgeGate.Application.Chat;

/// <summary>
/// Normalized retry semantics for provider failure.
/// Separate from category to allow independent evolution.
/// </summary>
public enum ProviderFailureRetryability
{
    NotRetryable,
    RetryImmediately,
    RetryAfterDelay,
    RetryViaAnotherRoute
}
