namespace ForgeGate.Application.Chat;

/// <summary>
/// Provider execution abstraction for chat completions.
/// Application owns this contract; Infrastructure implements it.
/// </summary>
public interface IChatCompletionProvider
{
    /// <summary>
    /// Executes a chat completion request against the provider.
    /// Returns normalized outcome to avoid leaking infrastructure exceptions.
    /// </summary>
    /// <param name="request">The canonical chat request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Application-owned normalized outcome.</returns>
    Task<ProviderExecutionOutcome> ExecuteAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken);
}