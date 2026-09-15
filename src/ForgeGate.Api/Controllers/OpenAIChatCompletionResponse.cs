namespace ForgeGate.Api.Controllers;

/// <summary>
/// OpenAI-compatible Chat Completion response DTO.
/// </summary>
public sealed class OpenAIChatCompletionResponse
{
    public required string Id { get; init; }
    public required string Object { get; init; }
    public required long Created { get; init; }
    public required string Model { get; init; }
    public required List<OpenAIChatCompletionChoice> Choices { get; init; } = new();
    public OpenAIUsage? Usage { get; init; }
}

public sealed class OpenAIChatCompletionChoice
{
    public required int Index { get; init; }
    public required OpenAIChatMessage Message { get; init; }
    public required string FinishReason { get; init; }
    
    /// <summary>
    /// Tool calls requested by the model. Present when FinishReason is "tool_calls".
    /// </summary>
    public List<OpenAIChatCompletionToolCall>? ToolCalls { get; init; }
}

/// <summary>
/// OpenAI-compatible representation of a tool call in a chat completion response.
/// </summary>
public sealed class OpenAIChatCompletionToolCall
{
    public required string Id { get; init; }
    public string Type { get; init; } = "function";
    public OpenAIChatCompletionToolCallFunction Function { get; init; } = null!;
}

public sealed class OpenAIChatCompletionToolCallFunction
{
    public required string Name { get; init; }
    public required string Arguments { get; init; }
}

public sealed class OpenAIUsage
{
    public required int PromptTokens { get; init; }
    public required int CompletionTokens { get; init; }
    public required int TotalTokens { get; init; }
}