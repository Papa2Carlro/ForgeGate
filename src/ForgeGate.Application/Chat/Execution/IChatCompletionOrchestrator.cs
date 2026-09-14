using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Execution;

/// <summary>
/// Interface for the chat completion orchestrator.
/// Orchestrates route resolution and execution with transparent failover support.
/// </summary>
public interface IChatCompletionOrchestrator
{
    /// <summary>
    /// Executes a chat completion request with transparent route failover.
    /// If the first selected route fails with a retry-via-another-route failure,
    /// the orchestrator will attempt to resolve and execute an alternate route
    /// within the same quality tier.
    /// </summary>
    /// <param name="request">The canonical chat request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The final chat completion outcome.</returns>
    Task<ChatCompletionOutcome> ExecuteAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken);
}
