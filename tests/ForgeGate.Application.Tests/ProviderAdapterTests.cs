using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests;

public class ProviderAdapterTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCanonicalResponse()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );
        var request = new CanonicalChatRequest
        {
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        // When
        var provider = new FakeOpenAIChatCompletionProvider();
        var outcome = await provider.ExecuteAsync(route, request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        var response = outcome.Response!;
        Assert.Equal("Mock response from provider", response.Content);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailure_ReturnsFailureOutcome()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );
        var request = new CanonicalChatRequest
        {
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var provider = new FailingChatCompletionProvider();

        // When
        var outcome = await provider.ExecuteAsync(route, request, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(ProviderFailureKind.RequestFailed, outcome.FailureKind);
    }

    private sealed class FakeOpenAIChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderExecutionOutcome.Success(new CanonicalChatResponse
            {
                Content = "Mock response from provider"
            }));
        }
    }

    private sealed class FailingChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderExecutionOutcome.RequestFailed("Provider failed", "Details"));
        }
    }
}