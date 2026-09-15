namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Canonical semantic capabilities that Agent Guard recognizes.
/// 
/// This enum is the SINGLE canonical source of capability identities.
/// ActionIntent, CapabilityDescriptor, and ICapabilityRegistry all
/// reference this enum — no duplicate taxonomy is permitted.
/// 
/// Capabilities are added here as the Capability Registry expands.
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public enum ActionIntentKind
{
    /// <summary>
    /// Unknown or unrecognized capability — the normalizer could not
    /// map the raw action to a known semantic intent.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Read a file from the filesystem.
    /// </summary>
    FileRead,

    /// <summary>
    /// Write or modify a file on the filesystem.
    /// </summary>
    FileWrite,

    /// <summary>
    /// List contents of a directory.
    /// </summary>
    DirectoryList,

    /// <summary>
    /// Search for files matching a pattern.
    /// </summary>
    FileSearch,

    /// <summary>
    /// Delete a file from the filesystem.
    /// </summary>
    FileDelete,

    /// <summary>
    /// Make an HTTP request to a remote endpoint.
    /// </summary>
    HttpRequest,

    /// <summary>
    /// Spawn a child process / execute a command.
    /// </summary>
    ProcessSpawn,
}
