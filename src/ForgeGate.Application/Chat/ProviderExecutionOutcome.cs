namespace ForgeGate.Application.Chat;

/// <summary>
/// Application-owned outcome of provider execution.
/// Normalizes provider failures before crossing application boundary.
/// </summary>
public sealed class ProviderExecutionOutcome
{
    private ProviderExecutionOutcome() { }

    public static ProviderExecutionOutcome Success(CanonicalChatResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static ProviderExecutionOutcome RequestFailed(string message, string? details = null) =>
        new() { IsSuccess = false, FailureKind = ProviderFailureKind.RequestFailed, ErrorMessage = message, ErrorDetails = details };

    public static ProviderExecutionOutcome ResponseUnusable(string message) =>
        new() { IsSuccess = false, FailureKind = ProviderFailureKind.ResponseUnusable, ErrorMessage = message };

    public bool IsSuccess { get; init; }
    public CanonicalChatResponse? Response { get; init; }
    public ProviderFailureKind? FailureKind { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorDetails { get; init; }
}

public enum ProviderFailureKind
{
    RequestFailed,
    ResponseUnusable
}
