using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.Execution;

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
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var expectedResponse = new CanonicalChatResponse
        {
            Content = "Hi there"
        };

        var mockProvider = new FakeChatCompletionProvider(expectedResponse);
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        var result = outcome.Response!;
        Assert.Equal(expectedResponse.Content, result.Content);
        Assert.Single(mockProvider.CapturedRequests);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Given
        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ExecuteAsync(null!, route, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_NullRoute_ThrowsArgumentNullException()
    {
        // Given
        var request = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ExecuteAsync(request, null!, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailure_ReturnsSameFailureWithoutReclassification()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        // Create a specific ProviderFailure to test
        var expectedFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ContextExceeded,
            Retryability = ProviderFailureRetryability.RetryAfterDelay,
            Scope = ProviderFailureScope.ModelRoute,
            UpstreamStatusCode = 400,
            UpstreamCode = "context_length_exceeded",
            SanitizedUpstreamMessage = "Context length exceeded",
            RetryAfter = TimeSpan.FromSeconds(30)
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(expectedFailure);
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Same(expectedFailure, outcome.FailureValue); // Same instance
        Assert.Equal(expectedFailure.Category, outcome.FailureValue!.Category);
        Assert.Equal(expectedFailure.Retryability, outcome.FailureValue!.Retryability);
        Assert.Equal(expectedFailure.Scope, outcome.FailureValue!.Scope);
        Assert.Equal(expectedFailure.UpstreamStatusCode, outcome.FailureValue!.UpstreamStatusCode);
        Assert.Equal(expectedFailure.UpstreamCode, outcome.FailureValue!.UpstreamCode);
        Assert.Equal(expectedFailure.SanitizedUpstreamMessage, outcome.FailureValue!.SanitizedUpstreamMessage);
        Assert.Equal(expectedFailure.RetryAfter, outcome.FailureValue!.RetryAfter);
    }
}
