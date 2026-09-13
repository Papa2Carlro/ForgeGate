using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat;

/// <summary>
/// Provider execution abstraction for chat completions.
/// Application owns this contract; Infrastructure implements it.
/// </summary>
public interface IChatCompletionProvider
{
    /// <summary>
    /// Executes a chat completion request against the provider with explicit route identity.
    /// Returns normalized outcome to avoid leaking infrastructure exceptions.
    /// </summary>
    /// <param name="route">Explicit model route to execute against.</param>
    /// <param name="request">The canonical chat request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Application-owned normalized outcome.</returns>
    Task<ProviderExecutionOutcome> ExecuteAsync(
        ModelRoute route,
        CanonicalChatRequest request,
        CancellationToken cancellationToken);
}