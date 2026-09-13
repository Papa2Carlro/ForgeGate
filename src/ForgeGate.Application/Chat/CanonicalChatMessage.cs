namespace ForgeGate.Application.Chat;

/// <summary>
/// Canonical representation of a chat message, protocol-neutral.
/// </summary>
public sealed record CanonicalChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}
