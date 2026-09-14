using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing;
using ForgeGate.Domain.Providers;

namespace ForgeGate.IntegrationTests;

public class ChatCompletionsIntegrationTests
{
    [Fact]
    public async Task VerticalIntegration_OpenAIRequestToProviderResponse_CompleteFlow()
    {
        // Given
        var openAIRequest = new OpenAIChatCompletionRequestDto
        {
            Model = "gpt-4",
            Messages = new List<OpenAIMessageDto>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4"));

        // When - Simulate controller flow
        var canonicalRequest = MapToCanonical(openAIRequest);
        var outcome = await service.ExecuteAsync(canonicalRequest, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        var canonicalResponse = outcome.Response!;
        var apiResponse = MapToApiResponse(canonicalResponse, route);

        // Then
        Assert.Equal("gpt-4", apiResponse.Model);
        Assert.Single(apiResponse.Choices);
        Assert.Equal("assistant", apiResponse.Choices[0].Message.Role);
        Assert.Equal("Mock response", apiResponse.Choices[0].Message.Content);
    }

    [Fact]
    public async Task VerticalIntegration_ProviderFailure_ReturnsError()
    {
        // Given
        var openAIRequest = new OpenAIChatCompletionRequestDto
        {
            Model = "gpt-4",
            Messages = new List<OpenAIMessageDto>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var failingProvider = new FailingChatCompletionProvider();
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4"));

        // When
        var canonicalRequest = MapToCanonical(openAIRequest);
        var outcome = await service.ExecuteAsync(canonicalRequest, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(ProviderFailureCategory.InvalidRequest, outcome.FailureValue!.Category);
    }

    private static CanonicalChatRequest MapToCanonical(OpenAIChatCompletionRequestDto request)
    {
        return new CanonicalChatRequest
        {
            RequestedModel = request.Model ?? string.Empty,
            Messages = request.Messages.Select(m => new CanonicalChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };
    }

    private static OpenAIChatCompletionResponseDto MapToApiResponse(CanonicalChatResponse canonicalResponse, ModelRoute route)
    {
        return new OpenAIChatCompletionResponseDto
        {
            Model = route.LogicalModelId.Value,
            Choices = new List<OpenAIChoiceDto>
            {
                new()
                {
                    Index = 0,
                    Message = new OpenAIMessageDto
                    {
                        Role = "assistant",
                        Content = canonicalResponse.Content
                    },
                    FinishReason = "stop"
                }
            }
        };
    }

    private sealed class FakeChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderExecutionOutcome.Success(new CanonicalChatResponse
            {
                Content = "Mock response"
            }));
        }
    }

    private sealed class FailingChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderExecutionOutcome.Failure(new ProviderFailure
            {
                Category = ProviderFailureCategory.InvalidRequest,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Request,
                SanitizedUpstreamMessage = "Provider failed"
            }));
        }
    }

    private sealed record OpenAIChatCompletionRequestDto
    {
        public required string Model { get; init; }
        public required List<OpenAIMessageDto> Messages { get; init; }
    }

    private sealed record OpenAIMessageDto
    {
        public required string Role { get; init; }
        public required string Content { get; init; }
    }

    private sealed record OpenAIChatCompletionResponseDto
    {
        public required string Model { get; init; }
        public required List<OpenAIChoiceDto> Choices { get; init; }
    }

    private sealed record OpenAIChoiceDto
    {
        public required int Index { get; init; }
        public required OpenAIMessageDto Message { get; init; }
        public required string FinishReason { get; init; }
    }
}