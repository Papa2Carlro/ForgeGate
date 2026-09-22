using System.Text.Json.Serialization;

namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// OpenAI-specific DTO for chat completion responses.
/// Belongs to Infrastructure layer; does not leak into Application.
/// </summary>
public sealed record OpenAIChatCompletionResponse
{
    public required string Id { get; init; }
    [JsonPropertyName("object")]
    public required string Object { get; init; }
    public required long Created { get; init; }
    public required string Model { get; init; }
    public required IReadOnlyList<OpenAIChatCompletionChoice> Choices { get; init; }
    public OpenAIUsage? Usage { get; init; }
}

public sealed record OpenAIChatCompletionChoice
{
    public required int Index { get; init; }
    public required OpenAIChatMessage Message { get; init; }
    [JsonPropertyName("finish_reason")]
    public required string FinishReason { get; init; }

    /// <summary>
    /// Tool calls requested by the model. Present when FinishReason is "tool_calls".
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<OpenAIChatCompletionToolCall>? ToolCalls { get; init; }
}

/// <summary>
/// OpenAI-specific representation of a tool call in a chat completion response.
/// Belongs to Infrastructure layer; does not leak into Application.
/// </summary>
public sealed record OpenAIChatCompletionToolCall
{
    public required string Id { get; init; }
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";
    public OpenAIChatCompletionToolCallFunction Function { get; init; } = null!;
}

public sealed record OpenAIChatCompletionToolCallFunction
{
    public required string Name { get; init; }
    public required string Arguments { get; init; }
}

public sealed record OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public required int PromptTokens { get; init; }
    [JsonPropertyName("completion_tokens")]
    public required int CompletionTokens { get; init; }
    [JsonPropertyName("total_tokens")]
    public required int TotalTokens { get; init; }
}