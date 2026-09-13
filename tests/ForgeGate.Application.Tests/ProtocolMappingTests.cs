using ForgeGate.Domain.Chat;

namespace ForgeGate.Application.Tests;

public class ProtocolMappingTests
{
    [Fact]
    public void OpenAIRequestToCanonical_MapsAllFields()
    {
        // Given
        var openAIRequest = new OpenAIChatCompletionRequestDto
        {
            Model = "gpt-4",
            Messages = new List<OpenAIMessageDto>
            {
                new() { Role = "system", Content = "You are helpful" },
                new() { Role = "user", Content = "Hello" }
            }
        };

        // When
        var canonical = MapToCanonical(openAIRequest);

        // Then
        Assert.Equal("gpt-4", canonical.Model);
        Assert.Equal(2, canonical.Messages.Count);
        Assert.Equal("system", canonical.Messages[0].Role);
        Assert.Equal("You are helpful", canonical.Messages[0].Content);
        Assert.Equal("user", canonical.Messages[1].Role);
        Assert.Equal("Hello", canonical.Messages[1].Content);
    }

    [Fact]
    public void CanonicalToOpenAIResponse_MapsAllFields()
    {
        // Given
        var canonical = new CanonicalChatResponse
        {
            Model = "gpt-4",
            Content = "Hi there!"
        };

        // When
        var openAIResponse = MapToApiResponse(canonical);

        // Then
        Assert.Equal("gpt-4", openAIResponse.Model);
        Assert.Single(openAIResponse.Choices);
        Assert.Equal("assistant", openAIResponse.Choices[0].Message.Role);
        Assert.Equal("Hi there!", openAIResponse.Choices[0].Message.Content);
        Assert.Equal("stop", openAIResponse.Choices[0].FinishReason);
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