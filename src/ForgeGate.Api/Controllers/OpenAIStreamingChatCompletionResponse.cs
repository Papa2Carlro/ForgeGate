namespace ForgeGate.Api.Controllers;

/// <summary>
/// OpenAI-compatible streaming chat completion response.
/// Each choice contains a delta message that accumulates across SSE events.
/// </summary>
public sealed class OpenAIStreamingChatCompletionResponse
{
    public string Id { get; init; } = string.Empty;
    public string Object { get; init; } = "chat.completion.chunk";
    public long Created { get; init; }
    public string Model { get; init; } = string.Empty;
    public List<OpenAIStreamingChatCompletionChoice> Choices { get; init; } = new();
    public OpenAIUsage? Usage { get; init; }
}

public sealed class OpenAIStreamingChatCompletionChoice
{
    public int Index { get; init; }
    public OpenAIChatMessageDelta? Delta { get; init; }
    public string? FinishReason { get; init; }
}

public sealed class OpenAIChatMessageDelta
{
    public string? Role { get; init; }
    public string? Content { get; init; }
    
    /// <summary>
    /// Tool calls delta chunks (accumulated across SSE events).
    /// TODO: Streaming tool call accumulation requires separate slice design.
    /// </summary>
    public List<OpenAIChatMessageDeltaToolCall>? ToolCalls { get; init; }
}

/// <summary>
/// Delta tool call in streaming response.
/// Belongs to API layer; maps to structured ToolCallInvocation in canonical form.
/// </summary>
public sealed class OpenAIChatMessageDeltaToolCall
{
    public string? Id { get; init; }
    public int? Index { get; init; }
    public OpenAIChatMessageDeltaToolCallFunction? Function { get; init; }
}

public sealed class OpenAIChatMessageDeltaToolCallFunction
{
    public string? Name { get; init; }
    public string? Arguments { get; init; }
}
