using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.TestDoubles;

/// <summary>
/// Test double that returns a specific failure for all executions.
/// </summary>
public sealed class FakeProviderThatReturnsSpecificFailure : IChatCompletionProvider
{
    private readonly ProviderFailure _failureToReturn;

    public FakeProviderThatReturnsSpecificFailure(ProviderFailure failureToReturn)
    {
        _failureToReturn = failureToReturn;
    }

    public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(ProviderExecutionOutcome.Failure(_failureToReturn));
    }
}
