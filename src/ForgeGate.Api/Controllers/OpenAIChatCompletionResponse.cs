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
}

public sealed class OpenAIUsage
{
    public required int PromptTokens { get; init; }
    public required int CompletionTokens { get; init; }
    public required int TotalTokens { get; init; }
}