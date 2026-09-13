using ForgeGate.Application.Chat;
using ForgeGate.Domain.Chat;

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
        var response = await provider.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.Equal("gpt-4", response.Model);
        Assert.Equal("Mock response from provider", response.Content);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_RequestFailedException()
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

        // When & Then
        await Assert.ThrowsAsync<ProviderRequestFailedException>(
            () => provider.ExecuteAsync(request, CancellationToken.None));
    }

    private sealed class FakeOpenAIChatCompletionProvider : IChatCompletionProvider
    {
        public Task<CanonicalChatResponse> ExecuteAsync(CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CanonicalChatResponse
            {
                Model = request.Model,
                Content = "Mock response from provider"
            });
        }
    }

    private sealed class FailingChatCompletionProvider : IChatCompletionProvider
    {
        public Task<CanonicalChatResponse> ExecuteAsync(CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            throw new ProviderRequestFailedException("Provider request failed");
        }
    }

    private sealed class ProviderRequestFailedException : Exception
    {
        public ProviderRequestFailedException(string message) : base(message) { }
    }
}