namespace ForgeGate.Application.Chat.Streaming;

/// <summary>
/// Represents the commit state of a streaming response buffer.
/// Distinguishes pre-commit (failover allowed) from committed (no silent splice).
/// </summary>
public enum StreamingCommitState
{
    /// <summary>
    /// Pre-commit phase: failover allowed, buffer accumulating tokens.
    /// </summary>
    PreCommit,

    /// <summary>
    /// Post-commit phase: silent failover NOT allowed, stream locked to route.
    /// </summary>
    Committed
}
