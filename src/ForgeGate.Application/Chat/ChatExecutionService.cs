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
    /// <returns>Application-owned normalized outcome.</returns>
    public async Task<ProviderExecutionOutcome> ExecuteAsync(
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
        var outcome = await _provider.ExecuteAsync(request, cancellationToken);
        
        if (!outcome.IsSuccess)
        {
            // Re-throw normalized failures as typed exceptions for API mapping
            // This keeps API boundary clean while preserving failure semantics
            switch (outcome.FailureKind)
            {
                case ProviderFailureKind.RequestFailed:
                    throw new ProviderRequestFailedException(outcome.ErrorMessage ?? "Provider request failed", outcome.ErrorDetails);
                case ProviderFailureKind.ResponseUnusable:
                    throw new ProviderResponseUnusableException(outcome.ErrorMessage ?? "Provider response unusable");
                default:
                    throw new InvalidOperationException("Unknown provider failure kind");
            }
        }

        return outcome;
    }
}

public sealed class ProviderRequestFailedException : Exception
{
    public ProviderRequestFailedException(string message, string? details = null)
        : base(message)
    {
        Details = details;
    }

    public string? Details { get; }
}

public sealed class ProviderResponseUnusableException : Exception
{
    public ProviderResponseUnusableException(string message)
        : base(message)
    {
    }
}