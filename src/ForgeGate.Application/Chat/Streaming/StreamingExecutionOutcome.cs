using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Streaming;

/// <summary>
/// Application-owned outcome of streaming provider execution.
/// Contains buffered chunks and commit state.
/// </summary>
public sealed class StreamingExecutionOutcome
{
    private StreamingExecutionOutcome() { }

    public static StreamingExecutionOutcome Success(IReadOnlyList<string> bufferedChunks, bool isCommitted, ModelRoute route) =>
        new()
        {
            IsSuccess = true,
            BufferedChunks = bufferedChunks ?? Array.Empty<string>(),
            IsCommitted = isCommitted,
            SelectedRoute = route
        };

    public static StreamingExecutionOutcome Failure(ProviderFailure failure) =>
        new()
        {
            IsSuccess = false,
            FailureValue = failure
        };

    public bool IsSuccess { get; init; }
    public IReadOnlyList<string> BufferedChunks { get; init; } = Array.Empty<string>();
    public bool IsCommitted { get; init; }
    public ProviderFailure? FailureValue { get; init; }
    public ModelRoute? SelectedRoute { get; init; }
}
