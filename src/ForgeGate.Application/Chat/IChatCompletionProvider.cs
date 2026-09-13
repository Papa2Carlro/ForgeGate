using ForgeGate.Domain.Chat;

namespace ForgeGate.Application.Chat;

/// <summary>
/// Provider execution abstraction for chat completions.
/// Application owns this contract; Infrastructure implements it.
/// </summary>
public interface IChatCompletionProvider
{
    /// <summary>
    /// Executes a chat completion request against the provider.
    /// </summary>
    /// <param name="request">The canonical chat request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The canonical chat response from the provider.</returns>
    Task<CanonicalChatResponse> ExecuteAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken);
}