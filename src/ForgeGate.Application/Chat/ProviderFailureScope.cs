namespace ForgeGate.Application.Chat;

/// <summary>
/// Normalized failure scope for provider execution.
/// Describes the extent of impact, independent of category or retryability.
/// </summary>
public enum ProviderFailureScope
{
    Request,
    ModelRoute,
    Provider,
    Credential,
    QuotaWindow,
    Unknown
}
