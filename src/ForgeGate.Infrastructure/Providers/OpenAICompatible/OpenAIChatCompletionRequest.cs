using System.Text.Json.Serialization;

namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// OpenAI-specific DTO for chat completion requests.
/// Belongs to Infrastructure layer; does not leak into Application.
/// </summary>
public sealed record OpenAIChatCompletionRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }
    [JsonPropertyName("messages")]
    public required IReadOnlyList<OpenAIChatMessage> Messages { get; init; }

    // Optional parameters that might be needed by the existing code path
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public double? TopP { get; init; }
    public double? FrequencyPenalty { get; init; }
    public double? PresencePenalty { get; init; }
    public bool? Stream { get; init; }
}