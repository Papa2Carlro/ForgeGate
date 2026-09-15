namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Normalized semantic representation of an agent's intended action.
/// 
/// This is the output of the Intent Normalization step:
///   Agent Action (raw) → Intent Normalization → ActionIntent
/// 
/// It is protocol-neutral and capability-oriented, not command-oriented.
/// Example: "run_terminal(&quot;cat /workspace/foo.txt&quot;)"
///   maps to → ActionIntent(FileRead, "/workspace/foo.txt")
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed record ActionIntent
{
    /// <summary>
    /// The canonical semantic capability being requested.
    /// </summary>
    public required ActionIntentKind Capability { get; init; }

    /// <summary>
    /// The target resource or path the capability operates on.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Optional supplementary metadata (scope, mechanism details, etc.).
    /// </summary>
    public string? Metadata { get; init; }
}
