namespace ForgeGate.Application.Chat;

/// <summary>
/// Canonical representation of a chat completion response, protocol-neutral.
/// </summary>
public sealed record CanonicalChatResponse
{
    public required string Model { get; init; }
    public required string Content { get; init; }
}
