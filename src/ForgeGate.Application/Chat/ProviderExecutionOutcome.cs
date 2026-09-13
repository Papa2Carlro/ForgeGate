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

    public static ProviderExecutionOutcome Failure(ProviderFailure failure) =>
        new() { IsSuccess = false, FailureValue = failure };

    public bool IsSuccess { get; init; }
    public CanonicalChatResponse? Response { get; init; }
    public ProviderFailure? FailureValue { get; init; }
}