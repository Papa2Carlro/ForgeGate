using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.TestDoubles;

/// <summary>
/// Test double that returns a fixed response for all executions.
/// </summary>
public sealed class FakeChatCompletionProvider : IChatCompletionProvider
{
    private readonly CanonicalChatResponse _response;
    public List<CanonicalChatRequest> CapturedRequests { get; } = new();
    public int CallCount { get; private set; }

    public FakeChatCompletionProvider(CanonicalChatResponse? response = null)
    {
        _response = response ?? new CanonicalChatResponse { Content = "test" };
    }

    public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);
        CallCount++;
        return Task.FromResult(ProviderExecutionOutcome.Success(_response));
    }
}
