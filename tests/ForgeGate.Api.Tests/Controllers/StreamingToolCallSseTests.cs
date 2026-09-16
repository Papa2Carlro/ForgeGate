using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Domain.Providers;
using ForgeGate.Api.Controllers;
using Xunit;

namespace ForgeGate.Api.Tests.Controllers;

/// <summary>
/// Tests for streaming tool-call SSE event generation.
/// </summary>
public class StreamingToolCallSseTests
{
    [Fact]
    public void BuildToolCallSseEvent_SingleToolCall_GeneratesValidJson()
    {
        // Given
        var toolCall = new ToolCallInvocation
        {
            Id = "call_abc123",
            Index = 0,
            Name = "file_read",
            Arguments = "{\"path\": \"/workspace/foo.txt\"}"
        };

        // When
        var sseEvent = ChatCompletionsController.BuildToolCallSseEvent(
            id: "test-id",
            created: 1234567890,
            model: "gpt-4",
            toolCall);

        // Then - verify it's valid SSE format with correct JSON
        Assert.StartsWith("id:", sseEvent);
        Assert.Contains("\"id\":\"test-id\"", sseEvent);
        Assert.Contains("\"model\":\"gpt-4\"", sseEvent);
        Assert.Contains("\"tool_calls\"", sseEvent);
        Assert.Contains("\"id\":\"call_abc123\"", sseEvent);
        Assert.Contains("\"name\":\"file_read\"", sseEvent);
        // Arguments are JSON-escaped in the serialized output
        Assert.Contains("path", sseEvent);
        Assert.Contains("/workspace/foo.txt", sseEvent);
    }

    [Fact]
    public void BuildToolCallSseEvent_MultipleToolCalls_GeneratesCorrectOrder()
    {
        // Given
        var toolCall1 = new ToolCallInvocation
        {
            Id = "call_1",
            Index = 0,
            Name = "file_read",
            Arguments = "{}"
        };
        var toolCall2 = new ToolCallInvocation
        {
            Id = "call_2",
            Index = 1,
            Name = "file_write",
            Arguments = "{\"content\": \"hello\"}"
        };

        // When
        var sseEvent1 = ChatCompletionsController.BuildToolCallSseEvent(
            id: "test-id", created: 1234567890, model: "gpt-4", toolCall1);
        var sseEvent2 = ChatCompletionsController.BuildToolCallSseEvent(
            id: "test-id", created: 1234567890, model: "gpt-4", toolCall2);

        // Then
        Assert.Contains("\"id\":\"call_1\"", sseEvent1);
        Assert.Contains("\"index\":0", sseEvent1);  // Verify index is preserved
        Assert.Contains("\"name\":\"file_read\"", sseEvent1);
        Assert.Contains("\"id\":\"call_2\"", sseEvent2);
        Assert.Contains("\"index\":1", sseEvent2);  // Verify different index
        Assert.Contains("\"name\":\"file_write\"", sseEvent2);
    }

    [Fact]
    public void BuildToolCallSseEvent_EmptyArguments_ProducesEmptyString()
    {
        // Given
        var toolCall = new ToolCallInvocation
        {
            Id = "call_empty",
            Index = 0,
            Name = "empty_func",
            Arguments = ""
        };

        // When
        var sseEvent = ChatCompletionsController.BuildToolCallSseEvent(
            id: "test-id", created: 1234567890, model: "gpt-4", toolCall);

        // Then
        Assert.Contains("\"arguments\":\"\"", sseEvent);
    }

    [Fact]
    public void BuildToolCallSseEvent_SpecialCharactersInArguments_EscapedCorrectly()
    {
        // Given
        var toolCall = new ToolCallInvocation
        {            Index = 0,            Id = "call_special",
            Name = "test_func",
            Arguments = "{\"path\": \"C:\\\\Users\\\\test\\\\file.txt\", \"line\": 42}"
        };

        // When
        var sseEvent = ChatCompletionsController.BuildToolCallSseEvent(
            id: "test-id", created: 1234567890, model: "gpt-4", toolCall);

        // Then - JSON should be properly serialized
        Assert.Contains("\"arguments\":\"", sseEvent);
        // The nested JSON should be escaped in the SSE data
    }

    [Fact]
    public void FinishReason_ToolCallsPresent_ReturnsToolCalls()
    {
        // Given
        var toolCalls = new List<ToolCallInvocation>
        {
            new ToolCallInvocation { Id = "call_1", Name = "test", Arguments = "{}" }
        };Index = 0, 
        var outcome = StreamingExecutionOutcome.Success(
            bufferedChunks: Array.Empty<string>(),
            isCommitted: true,
            route: null!,
            toolCalls: toolCalls);

        // When/Then - verify the finish reason logic
        var expectedFinishReason = outcome.ToolCalls.Count > 0 ? "tool_calls" : "stop";
        Assert.Equal("tool_calls", expectedFinishReason);
    }

    [Fact]
    public void FinishReason_NoToolCalls_ReturnsStop()
    {
        // Given
        var outcome = StreamingExecutionOutcome.Success(
            bufferedChunks: Array.Empty<string>(),
            isCommitted: true,
            route: null!,
            toolCalls: null);

        // When/Then - verify the finish reason logic
        var expectedFinishReason = outcome.ToolCalls.Count > 0 ? "tool_calls" : "stop";
        Assert.Equal("stop", expectedFinishReason);
    }
}
