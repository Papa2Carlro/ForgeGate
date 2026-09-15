using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Streaming;

/// <summary>
/// Provider execution abstraction for streaming chat completions.
/// Returns streaming chunks with commit state management.
/// 
/// Note: This interface requires an explicit route — it is intended for
/// low-level provider adapters (e.g. OpenAIStreamingChatCompletionProvider).
/// For the application-layer orchestrator that resolves routes internally,
/// use IStreamingChatCompletionOrchestrator instead.
/// </summary>
public interface IStreamingChatCompletionProvider
{
    /// <summary>
    /// Executes a streaming chat completion request against the provider.
    /// Buffers chunks in pre-commit phase; fails over transparently if RetryViaAnotherRoute.
    /// </summary>
    /// <param name="route">Explicit model route to execute against.</param>
    /// <param name="request">The canonical chat request with streaming enabled.</param>
    /// <param name="buffer">Buffer for collecting pre-commit chunks.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Streaming execution outcome with commit state.</returns>
    Task<StreamingExecutionOutcome> ExecuteStreamingAsync(
        ModelRoute route,
        StreamingChatRequest request,
        StreamingBuffer buffer,
        CancellationToken cancellationToken);
}

/// <summary>
/// Application-layer orchestrator for streaming chat completions.
/// Resolves routes internally and exposes a route-free interface for the API layer.
/// </summary>
public interface IStreamingChatCompletionOrchestrator
{
    /// <summary>
    /// Executes a streaming chat completion with internal route resolution and failover.
    /// </summary>
    Task<StreamingExecutionOutcome> ExecuteStreamingAsync(
        StreamingChatRequest request,
        StreamingBuffer buffer,
        CancellationToken cancellationToken);
}
