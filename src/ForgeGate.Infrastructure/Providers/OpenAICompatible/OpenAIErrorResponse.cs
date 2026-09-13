using System.Text.Json.Serialization;

namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

public sealed class OpenAIErrorResponse
{
    [JsonPropertyName("error")]
    public OpenAIError Error { get; init; } = new();
}

public sealed class OpenAIError
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("param")]
    public string? Param { get; init; }
}
