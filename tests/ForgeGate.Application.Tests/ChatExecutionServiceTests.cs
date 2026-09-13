using ForgeGate.Application.Chat;
using ForgeGate.Domain.Chat;

namespace ForgeGate.Application.Tests;

public class ChatExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_DelegatesToProvider()
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

        var expectedResponse = new CanonicalChatResponse
        {
            Model = "gpt-4",
            Content = "Hi there"
        };

        var mockProvider = new FakeChatCompletionProvider(expectedResponse);
        var service = new ChatExecutionService(mockProvider);

        // When
        var result = await service.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.Equal(expectedResponse.Model, result.Model);
        Assert.Equal(expectedResponse.Content, result.Content);
        Assert.Single(mockProvider.CapturedRequests);
        Assert.Equal(request.Model, mockProvider.CapturedRequests[0].Model);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Given
        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider);

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ExecuteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyModel_ThrowsArgumentException()
    {
        // Given
        var request = new CanonicalChatRequest
        {
            Model = "",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider);

        // When & Then
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(request, CancellationToken.None));
    }

    private sealed class FakeChatCompletionProvider : IChatCompletionProvider
    {
        private readonly CanonicalChatResponse _response;
        public List<CanonicalChatRequest> CapturedRequests { get; } = new();

        public FakeChatCompletionProvider(CanonicalChatResponse? response = null)
        {
            _response = response ?? new CanonicalChatResponse { Model = "test", Content = "test" };
        }

        public Task<CanonicalChatResponse> ExecuteAsync(CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            return Task.FromResult(_response);
        }
    }
}