namespace ForgeGate.Domain.AgentGuard;

/// <summary>
/// Immutable result of a capability translation attempt.
/// 
/// Distinguishes between:
/// - Success: the semantic intent was successfully translated to a
///   concrete capability action representation
/// - Failure: translation could not proceed (unknown capability, invalid
///   input, missing descriptor)
/// 
/// This result does NOT contain policy decisions (allow/deny), risk scores,
/// or authorization status — those belong to the Policy layer.
/// </summary>
public sealed class CapabilityTranslationResult
{
    private CapabilityTranslationResult() { }

    /// <summary>
    /// Translation succeeded — the capability is recognized and semantic
    /// fields are preserved.
    /// </summary>
    public static CapabilityTranslationResult Success(
        ActionIntentKind capability,
        string target,
        string? metadata = null) =>
        new()
        {
            IsSuccess = true,
            Capability = capability,
            Target = target,
            Metadata = metadata,
            FailureReason = null
        };

    /// <summary>
    /// Translation failed — the capability is unknown, unregistered, or
    /// the input semantic intent is invalid.
    /// </summary>
    public static CapabilityTranslationResult Failure(TranslationFailure reason) =>
        new()
        {
            IsSuccess = false,
            Capability = default,
            Target = string.Empty,
            Metadata = null,
            FailureReason = reason
        };

    public bool IsSuccess { get; init; }
    public ActionIntentKind Capability { get; init; }
    public string Target { get; init; } = string.Empty;
    public string? Metadata { get; init; }
    public TranslationFailure? FailureReason { get; init; }
}

/// <summary>
/// Carries the reason translation failed, for audit and debugging.
/// </summary>
public sealed record TranslationFailure(string Reason);
