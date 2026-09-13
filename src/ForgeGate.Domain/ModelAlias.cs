namespace ForgeGate.Domain;

/// <summary>
/// A logical model alias that maps to one or more concrete model identifiers.
/// Examples: "fast" → ["gpt-4o-mini", "claude-haiku"], "smart" → ["gpt-4o", "claude-sonnet"]
/// </summary>
public sealed class ModelAlias
{
    public required string Alias { get; init; }
    public required IReadOnlyList<string> Models { get; init; }
}
