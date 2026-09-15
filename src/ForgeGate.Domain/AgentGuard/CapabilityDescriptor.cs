namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Immutable descriptor for a semantic capability registered in the Capability Registry.
/// 
/// This is a DATA contract only — it describes WHAT a capability is, not HOW to
/// execute it or what POLICY applies to it.
/// 
/// Properties:
/// - Id: canonical identity (matches ActionIntentKind)
/// - Name: human-readable name
/// - Category: coarse-grained classification (e.g., "Filesystem", "Network")
/// - Description: plain-text description
/// - IsDestructive: true if the capability can irreversibly modify state
/// - IsExternal: true if the capability interacts with systems outside the workspace
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed record CapabilityDescriptor
{
    /// <summary>
    /// Canonical identity matching ActionIntentKind.
    /// </summary>
    public ActionIntentKind Id { get; init; }

    /// <summary>
    /// Human-readable name for UI/display purposes.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Coarse-grained category for grouping related capabilities.
    /// Examples: "Filesystem", "Directory", "Search", "Network", "Process"
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Plain-text description of what the capability does.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// True if the capability can irreversibly modify or destroy state.
    /// This is DESCRIPTIVE metadata only — it does NOT automatically deny.
    /// Policy decides Allow/Block.
    /// </summary>
    public bool IsDestructive { get; init; }

    /// <summary>
    /// True if the capability interacts with systems outside the local workspace.
    /// This is DESCRIPTIVE metadata only — it does NOT automatically block.
    /// Policy decides Allow/Block.
    /// </summary>
    public bool IsExternal { get; init; }

    /// <summary>
    /// Creates a FileRead descriptor.
    /// </summary>
    public static CapabilityDescriptor FileRead(string description = "Read a file from the filesystem") =>
        new()
        {
            Id = ActionIntentKind.FileRead,
            Name = "FileRead",
            Category = "Filesystem",
            Description = description,
            IsDestructive = false,
            IsExternal = false
        };

    /// <summary>
    /// Creates a FileWrite descriptor.
    /// </summary>
    public static CapabilityDescriptor FileWrite(string description = "Write or modify a file on the filesystem") =>
        new()
        {
            Id = ActionIntentKind.FileWrite,
            Name = "FileWrite",
            Category = "Filesystem",
            Description = description,
            IsDestructive = false,
            IsExternal = false
        };

    /// <summary>
    /// Creates a DirectoryList descriptor.
    /// </summary>
    public static CapabilityDescriptor DirectoryList(string description = "List contents of a directory") =>
        new()
        {
            Id = ActionIntentKind.DirectoryList,
            Name = "DirectoryList",
            Category = "Directory",
            Description = description,
            IsDestructive = false,
            IsExternal = false
        };

    /// <summary>
    /// Creates a FileSearch descriptor.
    /// </summary>
    public static CapabilityDescriptor FileSearch(string description = "Search for files matching a pattern") =>
        new()
        {
            Id = ActionIntentKind.FileSearch,
            Name = "FileSearch",
            Category = "Search",
            Description = description,
            IsDestructive = false,
            IsExternal = false
        };

    /// <summary>
    /// Creates a FileDelete descriptor.
    /// </summary>
    public static CapabilityDescriptor FileDelete(string description = "Delete a file from the filesystem") =>
        new()
        {
            Id = ActionIntentKind.FileDelete,
            Name = "FileDelete",
            Category = "Filesystem",
            Description = description,
            IsDestructive = true,
            IsExternal = false
        };

    /// <summary>
    /// Creates a HttpRequest descriptor.
    /// </summary>
    public static CapabilityDescriptor HttpRequest(string description = "Make an HTTP request to a remote endpoint") =>
        new()
        {
            Id = ActionIntentKind.HttpRequest,
            Name = "HttpRequest",
            Category = "Network",
            Description = description,
            IsDestructive = false,
            IsExternal = true
        };

    /// <summary>
    /// Creates a ProcessSpawn descriptor.
    /// </summary>
    public static CapabilityDescriptor ProcessSpawn(string description = "Spawn a child process or execute a command") =>
        new()
        {
            Id = ActionIntentKind.ProcessSpawn,
            Name = "ProcessSpawn",
            Category = "Process",
            Description = description,
            IsDestructive = false,
            IsExternal = false
        };
}
