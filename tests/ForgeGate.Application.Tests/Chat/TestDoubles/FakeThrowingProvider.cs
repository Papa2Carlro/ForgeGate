using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.TestDoubles;

/// <summary>
/// Test double that throws an unexpected exception.
/// </summary>
public sealed class FakeThrowingProvider : IChatCompletionProvider
{
    public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Unexpected error");
    }
}
