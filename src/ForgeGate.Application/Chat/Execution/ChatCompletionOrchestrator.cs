using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Execution;

/// <summary>
/// Application use-case coordinator for chat completion with transparent route failover.
/// 
/// This orchestrator:
/// 1. Resolves an initial route
/// 2. Executes the request against that route via ChatExecutionService
/// 3. If the execution fails with RetryViaAnotherRoute, excludes the failed route
///    and attempts to resolve an alternative route in the SAME quality tier
/// 4. Repeats until success or no more alternatives
/// 
/// Exhaustion contract:
/// - Initial resolution failure → returns resolution failure directly
/// - All routes in locked tier exhausted → returns NoEligibleRoute resolution failure
/// - Last provider failure is NOT returned after full exhaustion
/// - Terminal non-retryable provider failure → returned immediately without failover
/// 
/// It does NOT:
/// - Perform quality tier downgrade
/// - Retrial the same route
/// - Wait/backoff between attempts
/// - Cross logical model fallback
/// - Stream handling
/// </summary>
public sealed class ChatCompletionOrchestrator : IChatCompletionOrchestrator
{
    private readonly IRouteResolver _routeResolver;
    private readonly ChatExecutionService _chatExecutionService;

    public ChatCompletionOrchestrator(
        IRouteResolver routeResolver,
        ChatExecutionService chatExecutionService)
    {
        _routeResolver = routeResolver ?? throw new ArgumentNullException(nameof(routeResolver));
        _chatExecutionService = chatExecutionService ?? throw new ArgumentNullException(nameof(chatExecutionService));
    }

    public async Task<ChatCompletionOutcome> ExecuteAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var attemptedRoutes = new HashSet<ModelRouteId>();
        DeclaredQualityTier? lockedTier = null;
        ProviderExecutionOutcome? lastFailure = null;

        while (true)
        {
            // Create or reuse context with exclusions and locked tier
            var context = lockedTier.HasValue
                ? new RouteResolutionContext(request, attemptedRoutes.ToHashSet(), lockedTier)
                : RouteResolutionContext.CreateFresh(request);

            // Resolve route
            var resolution = await _routeResolver.ResolveAsync(context, cancellationToken);

            if (!resolution.IsSuccess)
            {
                // No more routes available

                return ChatCompletionOutcome.FailWithResolutionError(resolution.FailureValue!);
            }

            var route = resolution.Route!;

            // Check if we've already attempted this route (safety check)
            if (attemptedRoutes.Contains(route.ModelRouteId))
            {
                return lastFailure != null
                    ? ChatCompletionOutcome.Failure(lastFailure.FailureValue!)
                    : ChatCompletionOutcome.FailWithResolutionError(
                        RouteResolutionFailure.NoEligibleRoute(request.RequestedModel!));
            }

            // Lock the quality tier on first successful resolution
            if (!lockedTier.HasValue)
            {
                lockedTier = route.QualityTier;

            }
            else if (route.QualityTier != lockedTier.Value)
            {
                // This should not happen due to resolver enforcing tier lock
            }

            // Execute against the resolved route


            attemptedRoutes.Add(route.ModelRouteId);

            try
            {
                var outcome = await _chatExecutionService.ExecuteAsync(request, route, cancellationToken);

                if (outcome.IsSuccess)
                {

                    return ChatCompletionOutcome.Success(outcome.Response!, route);
                }

                // Execution failed - check if we should failover
                lastFailure = outcome;
                var failure = outcome.FailureValue;

                if (failure == null)
                {
                    // Should not happen, but treat as terminal

                    return ChatCompletionOutcome.Failure(new ProviderFailure
                    {
                        Category = ProviderFailureCategory.UnknownProviderFailure,
                        Retryability = ProviderFailureRetryability.NotRetryable,
                        Scope = ProviderFailureScope.ModelRoute,
                        SanitizedUpstreamMessage = "Unknown execution failure"
                    });
                }



                if (failure.Retryability != ProviderFailureRetryability.RetryViaAnotherRoute)
                {
                    // Terminal failure - do not failover

                    return ChatCompletionOutcome.Failure(failure);
                }

                // Continue to next attempt - the resolver will exclude this route

            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw;
            }
        }
    }
}
