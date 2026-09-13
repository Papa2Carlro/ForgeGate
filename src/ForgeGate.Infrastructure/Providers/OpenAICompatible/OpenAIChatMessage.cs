namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// OpenAI-specific DTO for chat completion messages.
/// Belongs to Infrastructure layer; does not leak into Application.
/// </summary>
public sealed record OpenAIChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}