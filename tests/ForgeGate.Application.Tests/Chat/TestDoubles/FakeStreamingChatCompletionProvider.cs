using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.TestDoubles;

/// <summary>
/// Test double for IStreamingChatCompletionProvider that returns success without chunks.
/// </summary>
public sealed class FakeStreamingChatCompletionProvider : IStreamingChatCompletionProvider
{
    public List<(ModelRoute Route, StreamingChatRequest Request, StreamingBuffer Buffer)> CapturedCalls { get; } = new();
    public int CallCount { get; private set; }

    public Task<StreamingExecutionOutcome> ExecuteStreamingAsync(
        ModelRoute route,
        StreamingChatRequest request,
        StreamingBuffer buffer,
        CancellationToken cancellationToken)
    {
        CapturedCalls.Add((route, request, buffer));
        CallCount++;
        return Task.FromResult(StreamingExecutionOutcome.Success(Array.Empty<string>(), false, route));
    }
}
