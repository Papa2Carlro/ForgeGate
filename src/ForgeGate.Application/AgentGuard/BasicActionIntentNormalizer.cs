using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Production-quality semantic intent normalizer for Agent Guard.
/// 
/// This normalizer uses explicit, deterministic parsing rules to map
/// raw agent actions to canonical ActionIntent. It is intentionally
/// conservative — only unambiguously recognizable patterns are translated;
/// everything else returns Unknown.
/// 
/// Supported patterns (minimal but extensible):
/// - "cat <path>" → FileRead
/// - "ls <path>" → DirectoryList
/// - "grep <query> <scope>" → FileSearch
/// 
/// Does NOT:
/// - Execute capabilities
/// - Make policy decisions
/// - Use fuzzy matching or substring heuristics
/// - Infer ProcessSpawn from incidental characters
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed class BasicActionIntentNormalizer : IActionIntentNormalizer
{
    private readonly ICapabilityRegistry _registry;

    public BasicActionIntentNormalizer(ICapabilityRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ActionIntentResult Normalize(AgentAction action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        var raw = action.RawAction.Trim();
        if (string.IsNullOrEmpty(raw))
            return ActionIntentResult.Unknown(raw);

        // Parse into command and arguments
        var (command, arguments) = ParseCommand(raw);

        // Normalize command to lowercase for matching
        var normalizedCommand = command?.ToLowerInvariant();

        return normalizedCommand switch
        {
            "cat" => NormalizeFileRead(arguments),
            "ls" => NormalizeDirectoryList(arguments),
            "grep" => NormalizeFileSearch(arguments),
            _ => ActionIntentResult.Unknown(raw)
        };
    }

    /// <summary>
    /// Parses a raw command string into command token and argument array.
    /// Handles leading/trailing whitespace but does NOT attempt full shell parsing.
    /// </summary>
    private static (string? command, string[] arguments) ParseCommand(string raw)
    {
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return (null, Array.Empty<string>());

        return (parts[0], parts.Skip(1).ToArray());
    }

    /// <summary>
    /// Normalizes "cat <path>" → FileRead(path).
    /// Requires exactly one argument (the file path).
    /// </summary>
    private static ActionIntentResult NormalizeFileRead(string[] arguments)
    {
        if (arguments.Length == 0)
            return ActionIntentResult.Unknown("cat (missing path)");

        if (arguments.Length > 1)
            return ActionIntentResult.Unknown("cat (too many arguments)");

        var path = arguments[0].Trim();
        if (string.IsNullOrEmpty(path))
            return ActionIntentResult.Unknown("cat (empty path)");

        return ActionIntentResult.Success(new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = path,
            Metadata = null
        });
    }

    /// <summary>
    /// Normalizes "ls <path>" → DirectoryList(path).
    /// Requires exactly one argument (the directory path).
    /// </summary>
    private static ActionIntentResult NormalizeDirectoryList(string[] arguments)
    {
        if (arguments.Length == 0)
            return ActionIntentResult.Unknown("ls (missing path)");

        if (arguments.Length > 1)
            return ActionIntentResult.Unknown("ls (too many arguments)");

        var path = arguments[0].Trim();
        if (string.IsNullOrEmpty(path))
            return ActionIntentResult.Unknown("ls (empty path)");

        return ActionIntentResult.Success(new ActionIntent
        {
            Capability = ActionIntentKind.DirectoryList,
            Target = path,
            Metadata = null
        });
    }

    /// <summary>
    /// Normalizes "grep <query> <scope>" → FileSearch(query, scope).
    /// Requires exactly two arguments (search query and target scope/path).
    /// </summary>
    private static ActionIntentResult NormalizeFileSearch(string[] arguments)
    {
        if (arguments.Length == 0)
            return ActionIntentResult.Unknown("grep (missing query)");

        if (arguments.Length < 2)
            return ActionIntentResult.Unknown("grep (missing scope)");

        if (arguments.Length > 2)
            return ActionIntentResult.Unknown("grep (too many arguments)");

        var query = arguments[0].Trim();
        var scope = arguments[1].Trim();

        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(scope))
            return ActionIntentResult.Unknown("grep (empty query or scope)");

        return ActionIntentResult.Success(new ActionIntent
        {
            Capability = ActionIntentKind.FileSearch,
            Target = scope,
            Metadata = query
        });
    }
}
