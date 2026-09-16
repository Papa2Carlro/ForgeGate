namespace ForgeGate.Application.Chat;

/// <summary>
/// Semantic representation of a provider-requested tool invocation.
/// 
/// This type captures the fact that the model/provider requested execution
/// of a tool with specific arguments. It is protocol-neutral and does not
/// depend on OpenAI-specific DTOs.
/// 
/// TODO: Future slice should construct this from provider responses and
/// feed it to Agent Guard for policy evaluation.
/// </summary>
public sealed record ToolCallInvocation
{
    /// <summary>
    /// Stable identifier for this tool call (if provided by provider).
    /// Used to correlate with tool result messages.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The index of this tool call in the provider's response.
    /// Used to correlate with the original streaming delta index.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// The name of the tool/function to invoke.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// JSON-encoded arguments for the tool invocation.
    /// Raw string, not parsed — parsed by the caller.
    /// </summary>
    public required string Arguments { get; init; }
}
