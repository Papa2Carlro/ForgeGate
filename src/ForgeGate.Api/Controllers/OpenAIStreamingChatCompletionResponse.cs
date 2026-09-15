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
}
