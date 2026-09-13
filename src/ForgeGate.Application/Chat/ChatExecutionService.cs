using ForgeGate.Domain.Chat;

namespace ForgeGate.Application.Chat;

/// <summary>
/// Application use case coordinator for chat execution.
/// Orchestrates the chat completion flow: validates input, delegates to provider, returns result.
/// </summary>
public sealed class ChatExecutionService
{
    private readonly IChatCompletionProvider _provider;

    public ChatExecutionService(IChatCompletionProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Executes a chat completion request through the provider.
    /// </summary>
    /// <param name="request">The canonical chat request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The canonical chat response from the provider.</returns>
    public Task<CanonicalChatResponse> ExecuteAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken)
    {
        // Application layer validation - basic null checks
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        
        if (string.IsNullOrWhiteSpace(request.Model))
            throw new ArgumentException("Model cannot be null or empty", nameof(request.Model));
        
        if (request.Messages == null || request.Messages.Count == 0)
            throw new ArgumentException("Messages cannot be null or empty", nameof(request.Messages));

        // Delegate to provider - Application does not contain provider-specific logic
        return _provider.ExecuteAsync(request, cancellationToken);
    }
}