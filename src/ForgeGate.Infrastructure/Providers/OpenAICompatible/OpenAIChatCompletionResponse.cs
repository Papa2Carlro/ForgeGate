namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// OpenAI-specific DTO for chat completion responses.
/// Belongs to Infrastructure layer; does not leak into Application.
/// </summary>
public sealed record OpenAIChatCompletionResponse
{
    public required string Id { get; init; }
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
    public required string FinishReason { get; init; }
}

public sealed record OpenAIUsage
{
    public required int PromptTokens { get; init; }
    public required int CompletionTokens { get; init; }
    public required int TotalTokens { get; init; }
}