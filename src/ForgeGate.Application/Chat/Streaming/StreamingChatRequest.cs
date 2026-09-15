namespace ForgeGate.Application.Chat.Streaming;

/// <summary>
/// Streaming-specific chat request that wraps the canonical request.
/// Contains streaming state and buffer management.
/// </summary>
public sealed class StreamingChatRequest
{
    public CanonicalChatRequest BaseRequest { get; init; } = null!;
    public bool Stream { get; init; } = false;
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
}
