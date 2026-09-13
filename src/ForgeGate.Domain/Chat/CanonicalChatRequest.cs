namespace ForgeGate.Domain.Chat;

/// <summary>
/// Canonical representation of a chat completion request, protocol-neutral.
/// </summary>
public sealed record CanonicalChatRequest
{
    public required string Model { get; init; }
    public required IReadOnlyList<CanonicalChatMessage> Messages { get; init; }
}