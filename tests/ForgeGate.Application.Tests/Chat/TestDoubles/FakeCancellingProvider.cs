using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.TestDoubles;

/// <summary>
/// Test double that throws OperationCanceledException when cancellation is requested.
/// </summary>
public sealed class FakeCancellingProvider : IChatCompletionProvider
{
    public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProviderExecutionOutcome.Success(new CanonicalChatResponse { Content = "never reached" }));
    }
}
