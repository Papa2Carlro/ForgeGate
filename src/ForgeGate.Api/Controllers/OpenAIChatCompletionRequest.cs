namespace ForgeGate.Api.Controllers;

/// <summary>
/// OpenAI-compatible Chat Completion request DTO.
/// Belongs to API layer; maps to canonical model.
/// </summary>
public sealed class OpenAIChatCompletionRequest
{
    public required string Model { get; init; }
    public required List<OpenAIChatMessage> Messages { get; init; } = new();
    
    // Optional parameters for compatibility
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public double? TopP { get; init; }
    public double? FrequencyPenalty { get; init; }
    public double? PresencePenalty { get; init; }
    public bool? Stream { get; init; }
    public List<OpenAITool> Tools { get; init; } = new();
    public string? ToolChoice { get; init; }
}

/// <summary>
/// OpenAI-compatible Chat Completion message DTO.
/// </summary>
public sealed class OpenAIChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}