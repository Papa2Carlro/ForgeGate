using ForgeGate.Application.Chat;
using ForgeGate.Domain.Chat;

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
        var service = new ChatExecutionService(mockProvider);

        // When - Simulate controller flow
        var canonicalRequest = MapToCanonical(openAIRequest);
        var canonicalResponse = await service.ExecuteAsync(canonicalRequest, CancellationToken.None);
        var apiResponse = MapToApiResponse(canonicalResponse);

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
        var service = new ChatExecutionService(failingProvider);

        // When & Then
        var canonicalRequest = MapToCanonical(openAIRequest);
        await Assert.ThrowsAsync<ForgeGate.Infrastructure.Providers.OpenAICompatible.ProviderRequestFailedException>(
            () => service.ExecuteAsync(canonicalRequest, CancellationToken.None));
    }

    private static CanonicalChatRequest MapToCanonical(OpenAIChatCompletionRequestDto request)
    {
        return new CanonicalChatRequest
        {
            Model = request.Model,
            Messages = request.Messages.Select(m => new CanonicalChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };
    }

    private static OpenAIChatCompletionResponseDto MapToApiResponse(CanonicalChatResponse canonicalResponse)
    {
        return new OpenAIChatCompletionResponseDto
        {
            Model = canonicalResponse.Model,
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
        public Task<CanonicalChatResponse> ExecuteAsync(CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CanonicalChatResponse
            {
                Model = request.Model,
                Content = "Mock response"
            });
        }
    }

    private sealed class FailingChatCompletionProvider : IChatCompletionProvider
    {
        public Task<CanonicalChatResponse> ExecuteAsync(CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            throw new ForgeGate.Infrastructure.Providers.OpenAICompatible.ProviderRequestFailedException("Provider failed");
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