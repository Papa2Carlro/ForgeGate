using ForgeGate.Application.Chat.Routing;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat;

/// <summary>
/// Application use case coordinator for chat execution.
/// Orchestrates the chat completion flow: validates input, delegates to provider, returns result.
/// </summary>
public sealed class ChatExecutionService
{
    private readonly IChatCompletionProvider _provider;
    private readonly IRouteHealthFeedback _healthFeedback;

    public ChatExecutionService(IChatCompletionProvider provider, IRouteHealthFeedback healthFeedback)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _healthFeedback = healthFeedback ?? throw new ArgumentNullException(nameof(healthFeedback));
    }

    /// <summary>
    /// Executes a chat completion request through the provider with explicit route identity.
    /// </summary>
    /// <param name="request">The canonical chat request.</param>
    /// <param name="route">Explicit model route to execute against.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Application-owned normalized outcome.</returns>
    public async Task<ProviderExecutionOutcome> ExecuteAsync(
        CanonicalChatRequest request,
        ModelRoute route,
        CancellationToken cancellationToken)
    {
        // Application layer validation - basic null checks
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        
        if (route == null)
            throw new ArgumentNullException(nameof(route));
        
        if (request.Messages == null || request.Messages.Count == 0)
            throw new ArgumentException("Messages cannot be null or empty", nameof(request.Messages));

        // Delegate to provider - Application does not contain provider-specific logic
        var outcome = await _provider.ExecuteAsync(route, request, cancellationToken);

        // Passive health feedback: observe outcome without altering it
        if (outcome.IsSuccess)
            _healthFeedback.RecordSuccess(route);
        else if (outcome.FailureValue != null)
            _healthFeedback.RecordFailure(route, outcome.FailureValue);

        // Provider failures are expected operational outcomes, returned as typed outcome
        // Do NOT throw for expected provider failures
        return outcome;
    }
}