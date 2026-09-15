using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Providers.OpenAICompatible;

namespace ForgeGate.Application.Tests.Chat;

/// <summary>
/// Tests for semantic tool-call mapping from provider to canonical model.
/// These tests verify the provider-to-canonical mapping layer.
/// For API-response mapping, see integration tests in ForgeGate.IntegrationTests.
/// </summary>
public class ToolCallMappingTests
{
    [Fact]
    public void MapToCanonicalResponse_WithToolCalls_PreservesIdNameArguments()
    {
        // Given
        var openaiResponse = new OpenAIChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4",
            Choices = new[]
            {
                new OpenAIChatCompletionChoice
                {
                    Index = 0,
                    Message = new OpenAIChatMessage { Role = "assistant", Content = "" },
                    FinishReason = "tool_calls",
                    ToolCalls = new[]
                    {
                        new OpenAIChatCompletionToolCall
                        {
                            Id = "call_abc123",
                            Function = new OpenAIChatCompletionToolCallFunction
                            {
                                Name = "file_read",
                                Arguments = "{\"path\": \"/workspace/foo.txt\"}"
                            }
                        }
                    }
                }
            }
        };

        // When
        var canonical = OpenAIChatCompletionProvider.MapToCanonicalResponseForTest(
            route: null!, 
            response: openaiResponse);

        // Then
        Assert.Empty(canonical.Content);
        Assert.Single(canonical.ToolCalls);
        Assert.Equal("call_abc123", canonical.ToolCalls[0].Id);
        Assert.Equal("file_read", canonical.ToolCalls[0].Name);
        Assert.Equal("{\"path\": \"/workspace/foo.txt\"}", canonical.ToolCalls[0].Arguments);
    }

    [Fact]
    public void MapToCanonicalResponse_WithMultipleToolCalls_PreservesAll()
    {
        // Given
        var openaiResponse = new OpenAIChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4",
            Choices = new[]
            {
                new OpenAIChatCompletionChoice
                {
                    Index = 0,
                    Message = new OpenAIChatMessage { Role = "assistant", Content = "" },
                    FinishReason = "tool_calls",
                    ToolCalls = new[]
                    {
                        new OpenAIChatCompletionToolCall
                        {
                            Id = "call_1",
                            Function = new OpenAIChatCompletionToolCallFunction
                            {
                                Name = "file_read",
                                Arguments = "{\"path\": \"/a.txt\"}"
                            }
                        },
                        new OpenAIChatCompletionToolCall
                        {
                            Id = "call_2",
                            Function = new OpenAIChatCompletionToolCallFunction
                            {
                                Name = "file_write",
                                Arguments = "{\"path\": \"/b.txt\", \"content\": \"hello\"}"
                            }
                        }
                    }
                }
            }
        };

        // When
        var canonical = OpenAIChatCompletionProvider.MapToCanonicalResponseForTest(
            route: null!, 
            response: openaiResponse);

        // Then
        Assert.Equal(2, canonical.ToolCalls.Count);
        Assert.Equal("call_1", canonical.ToolCalls[0].Id);
        Assert.Equal("file_read", canonical.ToolCalls[0].Name);
        Assert.Equal("call_2", canonical.ToolCalls[1].Id);
        Assert.Equal("file_write", canonical.ToolCalls[1].Name);
    }

    [Fact]
    public void MapToCanonicalResponse_WithoutToolCalls_ReturnsEmptyList()
    {
        // Given
        var openaiResponse = new OpenAIChatCompletionResponse
        {
            Id = "chatcmpl-123",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "gpt-4",
            Choices = new[]
            {
                new OpenAIChatCompletionChoice
                {
                    Index = 0,
                    Message = new OpenAIChatMessage { Role = "assistant", Content = "Hello world" },
                    FinishReason = "stop"
                }
            }
        };

        // When
        var canonical = OpenAIChatCompletionProvider.MapToCanonicalResponseForTest(
            route: null!, 
            response: openaiResponse);

        // Then
        Assert.Equal("Hello world", canonical.Content);
        Assert.Empty(canonical.ToolCalls);
    }

    [Fact]
    public void MapToCanonicalResponse_MissingToolCallId_ThrowsOrSkips()
    {
        // Given - missing required Id field should cause deserialization to fail
        var json = @"{
            ""id"": ""chatcmpl-123"",
            ""object"": ""chat.completion"",
            ""created"": 1234567890,
            ""model"": ""gpt-4"",
            ""choices"": [{
                ""index"": 0,
                ""message"": {""role"": ""assistant"", ""content"": ""test""},
                ""finish_reason"": ""tool_calls"",
                ""tool_calls"": [{
                    ""type"": ""function"",
                    ""function"": {
                        ""name"": ""file_read"",
                        ""arguments"": ""{}""
                    }
                }]
            }]
        }";

        // When/Then - should throw because Id is required
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
    }
}
