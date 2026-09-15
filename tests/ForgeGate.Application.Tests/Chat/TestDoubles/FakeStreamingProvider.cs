using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.TestDoubles;

/// <summary>
/// Test double that implements both IChatCompletionProvider and IStreamingChatCompletionProvider.
/// 
/// Constructors:
/// - FakeStreamingProvider(chunks) - always succeeds with chunks
/// - FakeStreamingProvider(failure) - always fails with the given failure  
/// - FakeStreamingProvider(chunks, failFirstN) - fails first N calls, then succeeds with chunks
/// </summary>
public sealed class FakeStreamingProvider : IChatCompletionProvider, IStreamingChatCompletionProvider
{
    private readonly IReadOnlyList<string> _chunks;
    private readonly ProviderFailure? _failure;
    private readonly bool _alwaysFail;
    private int _remainingFailures;
    public List<(ModelRoute Route, CanonicalChatRequest Request)> CapturedNonStreamingCalls { get; } = new();
    public List<(ModelRoute Route, StreamingChatRequest Request, StreamingBuffer Buffer)> CapturedStreamingCalls { get; } = new();
    public int NonStreamingCallCount { get; private set; }
    public int StreamingCallCount { get; private set; }

    public FakeStreamingProvider(IReadOnlyList<string> chunks, int failFirstN = 0)
    {
        _chunks = chunks ?? Array.Empty<string>();
        _failure = null;
        _alwaysFail = false;
        _remainingFailures = failFirstN;
    }

    public FakeStreamingProvider(ProviderFailure failure, int failFirstN = 0)
    {
        _chunks = Array.Empty<string>();
        _failure = failure;
        _remainingFailures = failFirstN;
        _alwaysFail = failFirstN == 0;
    }

    public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
    {
        CapturedNonStreamingCalls.Add((route, request));
        NonStreamingCallCount++;

        if (_alwaysFail)
            return Task.FromResult(ProviderExecutionOutcome.Failure(_failure!));

        return Task.FromResult(ProviderExecutionOutcome.Success(new CanonicalChatResponse { Content = "mock" }));
    }

    public Task<StreamingExecutionOutcome> ExecuteStreamingAsync(
        ModelRoute route,
        StreamingChatRequest request,
        StreamingBuffer buffer,
        CancellationToken cancellationToken)
    {
        CapturedStreamingCalls.Add((route, request, buffer));
        StreamingCallCount++;

        // Always fail mode (failure with failFirstN=0)
        if (_alwaysFail)
        {
            return Task.FromResult(StreamingExecutionOutcome.Failure(_failure!));
        }

        // Fail first N calls, then succeed with chunks
        if (_remainingFailures > 0)
        {
            _remainingFailures--;
            return Task.FromResult(StreamingExecutionOutcome.Failure(new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
                Scope = ProviderFailureScope.Provider,
                SanitizedUpstreamMessage = "Simulated failure"
            }));
        }

        foreach (var chunk in _chunks)
        {
            buffer.TryAddChunk(chunk);
        }

        if (_chunks.Count > 0)
        {
            buffer.Commit();
        }

        return Task.FromResult(StreamingExecutionOutcome.Success(_chunks, buffer.IsCommitted, route));
    }
}
