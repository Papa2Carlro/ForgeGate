using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Execution;

/// <summary>
/// Application-owned chat completion outcome.
/// Represents the final result of an orchestrated chat completion request.
/// </summary>
public sealed class ChatCompletionOutcome
{
    private ChatCompletionOutcome(bool isSuccess, CanonicalChatResponse? response, ProviderFailure? failure, ModelRoute? selectedRoute)
    {
        IsSuccess = isSuccess;
        Response = response;
        FailureValue = failure;
        SelectedRoute = selectedRoute;
    }

    public bool IsSuccess { get; }
    public CanonicalChatResponse? Response { get; }
    public ProviderFailure? FailureValue { get; }
    public ModelRoute? SelectedRoute { get; }

    public static ChatCompletionOutcome Success(CanonicalChatResponse response, ModelRoute route) =>
        new(true, response, null, route);

    public static ChatCompletionOutcome Failure(ProviderFailure failure) =>
        new(false, null, failure, null);

    public static ChatCompletionOutcome FailWithResolutionError(RouteResolutionFailure failure) =>
        new(false, null, new ProviderFailure
        {
            Category = ProviderFailureCategory.UnknownProviderFailure,
            Retryability = ProviderFailureRetryability.NotRetryable,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = failure.Details ?? "Route resolution failed"
        }, null);
}
