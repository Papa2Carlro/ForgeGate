using System.Text.Json.Serialization;

namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// OpenAI-specific DTO for chat completion messages.
/// Belongs to Infrastructure layer; does not leak into Application.
/// </summary>
public sealed record OpenAIChatMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}