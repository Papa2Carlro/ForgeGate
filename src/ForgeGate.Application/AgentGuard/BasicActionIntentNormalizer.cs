using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Minimal concrete normalizer for Slice 18 foundation.
/// 
/// CRITICAL DESIGN CONSTRAINT: This normalizer must NOT infer semantic
/// capabilities from weak syntactic heuristics (e.g., presence of "/"
/// or arbitrary command patterns). Only unambiguously recognizable
/// action patterns may be translated.
/// 
/// Current recognized patterns (minimal proof set):
/// - "cat <path>" → FileRead(<path>)
/// 
/// Everything else returns Unknown to avoid incorrect capability inference.
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed class BasicActionIntentNormalizer : IActionIntentNormalizer
{
    public ActionIntentResult Normalize(AgentAction action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        var raw = action.RawAction.Trim();
        if (string.IsNullOrEmpty(raw))
            return ActionIntentResult.Unknown(raw);

        // Canonical proof case: cat reads a file
        // This is the ONLY pattern we confidently recognize in this foundation slice.
        // Do NOT expand to other read commands (type, more, etc.) without explicit
        // semantic justification in a future slice.
        const string catPattern = "cat ";
        if (raw.StartsWith(catPattern, StringComparison.OrdinalIgnoreCase))
        {
            var target = raw.Substring(catPattern.Length).Trim();
            if (!string.IsNullOrEmpty(target))
            {
                return ActionIntentResult.Success(new ActionIntent
                {
                    Capability = ActionIntentKind.FileRead,
                    Target = target,
                    Metadata = action.Payload
                });
            }
        }

        // Default: Unknown — do NOT infer capability from incidental characters
        return ActionIntentResult.Unknown(raw);
    }
}
