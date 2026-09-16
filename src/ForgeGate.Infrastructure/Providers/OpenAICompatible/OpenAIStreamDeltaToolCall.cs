namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// Streaming delta tool call from OpenAI-compatible provider.
/// Belongs to Infrastructure layer; does not leak into Application.
/// </summary>
public sealed class OpenAIStreamDeltaToolCall
{
    public string? Id { get; init; }
    public int? Index { get; init; }
    public OpenAIStreamDeltaToolCallFunction? Function { get; init; }
}

public sealed class OpenAIStreamDeltaToolCallFunction
{
    public string? Name { get; init; }
    public string? Arguments { get; init; }
}
