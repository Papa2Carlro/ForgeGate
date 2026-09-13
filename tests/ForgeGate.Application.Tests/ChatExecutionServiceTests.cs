using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests;

public class ChatExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_DelegatesToProvider()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );

        var request = new CanonicalChatRequest
        {
            Route = route,
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var expectedResponse = new CanonicalChatResponse
        {
            Route = route,
            Content = "Hi there"
        };

        var mockProvider = new FakeChatCompletionProvider(expectedResponse);
        var service = new ChatExecutionService(mockProvider);

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        var result = outcome.Response!;
        Assert.Equal(expectedResponse.Content, result.Content);
        Assert.Single(mockProvider.CapturedRequests);
        Assert.Same(route, mockProvider.CapturedRequests[0].Route);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Given
        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider);
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ExecuteAsync(null!, route, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_NullRoute_ThrowsArgumentException()
    {
        // Given
        var request = new CanonicalChatRequest
        {
            Route = ModelRoute.FromIds(ProviderId.From("openai"), LogicalModelId.From("gpt-4"), ModelRouteId.From("openai:gpt-4")),
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider);

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ExecuteAsync(request, null!, CancellationToken.None));
    }

    private sealed class FakeChatCompletionProvider : IChatCompletionProvider
    {
        private readonly CanonicalChatResponse _response;
        public List<CanonicalChatRequest> CapturedRequests { get; } = new();

        public FakeChatCompletionProvider(CanonicalChatResponse? response = null)
        {
            var route = ModelRoute.FromIds(
                ProviderId.From("openai"),
                LogicalModelId.From("gpt-4"),
                ModelRouteId.From("openai:gpt-4")
            );
            _response = response ?? new CanonicalChatResponse { Route = route, Content = "test" };
        }

        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            return Task.FromResult(ProviderExecutionOutcome.Success(_response));
        }
    }
}