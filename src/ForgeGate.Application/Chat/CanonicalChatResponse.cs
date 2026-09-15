namespace ForgeGate.Application.Chat;

/// <summary>
/// Canonical representation of a chat completion response, protocol-neutral.
/// </summary>
public sealed record CanonicalChatResponse
{
    public required string Content { get; init; }
    
    /// <summary>
    /// Tool invocations requested by the provider.
    /// Empty when the response is pure text (no tool calls).
    /// </summary>
    public IReadOnlyList<ToolCallInvocation> ToolCalls { get; init; } = Array.Empty<ToolCallInvocation>();
}
