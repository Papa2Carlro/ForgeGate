using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat;

/// <summary>
/// Canonical representation of a chat completion request, protocol-neutral.
/// </summary>
public sealed record CanonicalChatRequest
{
    public required ModelRoute Route { get; init; }
    public required IReadOnlyList<CanonicalChatMessage> Messages { get; init; }
}
