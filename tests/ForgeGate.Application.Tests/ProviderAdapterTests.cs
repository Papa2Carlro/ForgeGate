using ForgeGate.Application.Chat;

namespace ForgeGate.Application.Tests;

public class ProviderAdapterTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCanonicalResponse()
    {
        // Given
        var request = new CanonicalChatRequest
        {
            Model = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        // When
        var provider = new FakeOpenAIChatCompletionProvider();
        var outcome = await provider.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        var response = outcome.Response!;
        Assert.Equal("gpt-4", response.Model);
        Assert.Equal("Mock response from provider", response.Content);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailure_ReturnsFailureOutcome()
    {
        // Given
        var request = new CanonicalChatRequest
        {
            Model = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var provider = new FailingChatCompletionProvider();

        // When
        var outcome = await provider.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(ProviderFailureKind.RequestFailed, outcome.FailureKind);
    }

    private sealed class FakeOpenAIChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderExecutionOutcome.Success(new CanonicalChatResponse
            {
                Model = request.Model,
                Content = "Mock response from provider"
            }));
        }
    }

    private sealed class FailingChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderExecutionOutcome.RequestFailed("Provider failed", "Details"));
        }
    }
}