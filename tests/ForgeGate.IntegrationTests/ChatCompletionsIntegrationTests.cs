using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Tests.Chat.TestDoubles;
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
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
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
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
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

    [Fact]
    public async Task VerticalIntegration_ProviderToolCalls_ToApiToolCalls_Preserved()
    {
        // Given
        var mockProvider = new ToolCallingChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4"));

        // When
        var canonicalRequest = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Read file" } }
        };
        var outcome = await service.ExecuteAsync(canonicalRequest, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        var apiResponse = MapToApiResponse(outcome.Response!, route);

        // Then
        Assert.Equal("tool_calls", apiResponse.Choices[0].FinishReason);
        Assert.NotNull(apiResponse.Choices[0].ToolCalls);
        Assert.Single(apiResponse.Choices[0].ToolCalls!);
        Assert.Equal("call_abc123", apiResponse.Choices[0].ToolCalls![0].Id);
        Assert.Equal("file_read", apiResponse.Choices[0].ToolCalls![0].Function.Name);
        Assert.Equal("{\"path\": \"/workspace/foo.txt\"}", apiResponse.Choices[0].ToolCalls![0].Function.Arguments);
    }

    [Fact]
    public async Task VerticalIntegration_OldResponseBehavior_Unchanged()
    {
        // Given
        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4"));

        // When
        var canonicalRequest = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var outcome = await service.ExecuteAsync(canonicalRequest, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        var apiResponse = MapToApiResponse(outcome.Response!, route);

        // Then - existing behavior preserved
        Assert.Equal("stop", apiResponse.Choices[0].FinishReason);
        Assert.Null(apiResponse.Choices[0].ToolCalls);
        Assert.Equal("Mock response", apiResponse.Choices[0].Message.Content);
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
        var finishReason = canonicalResponse.ToolCalls.Count > 0 ? "tool_calls" : "stop";
        
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
                    FinishReason = finishReason,
                    ToolCalls = canonicalResponse.ToolCalls.Count > 0
                        ? canonicalResponse.ToolCalls.Select(MapToApiToolCall).ToList()
                        : null
                }
            }
        };
    }

    private static OpenAIChoiceToolCall MapToApiToolCall(ToolCallInvocation invocation)
    {
        return new OpenAIChoiceToolCall
        {
            Id = invocation.Id,
            Function = new OpenAIChoiceToolCallFunction
            {
                Name = invocation.Name,
                Arguments = invocation.Arguments
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

    private sealed class ToolCallingChatCompletionProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderExecutionOutcome.Success(new CanonicalChatResponse
            {
                Content = "",
                ToolCalls = new[]
                {
                    new ToolCallInvocation
                    {
                        Id = "call_abc123",
                        Name = "file_read",
                        Arguments = "{\"path\": \"/workspace/foo.txt\"}"
                    }
                }
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
        public List<OpenAIChoiceToolCall>? ToolCalls { get; init; }
    }

    private sealed record OpenAIChoiceToolCall
    {
        public required string Id { get; init; }
        public OpenAIChoiceToolCallFunction Function { get; init; } = null!;
    }

    private sealed record OpenAIChoiceToolCallFunction
    {
        public required string Name { get; init; }
        public required string Arguments { get; init; }
    }
}