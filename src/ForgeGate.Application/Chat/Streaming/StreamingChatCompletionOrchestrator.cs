using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Streaming;

/// <summary>
/// Application use-case coordinator for streaming chat completion with transparent route failover.
/// 
/// This orchestrator:
/// 1. Resolves an initial route (with tier locking)
/// 2. Executes streaming against that route via ChatExecutionService
/// 3. If pre-commit phase fails with RetryViaAnotherRoute, excludes the failed route
///    and attempts to resolve an alternative route in the SAME quality tier
/// 4. After commit, no silent failover allowed
/// 
/// It does NOT:
/// - Perform quality tier downgrade
/// - Retrial the same route
/// - Wait/backoff between attempts
/// - Cross logical model fallback
/// </summary>
public sealed class StreamingChatCompletionOrchestrator : IStreamingChatCompletionOrchestrator
{
    private readonly IRouteResolver _routeResolver;
    private readonly ChatExecutionService _chatExecutionService;

    public StreamingChatCompletionOrchestrator(
        IRouteResolver routeResolver,
        ChatExecutionService chatExecutionService)
    {
        _routeResolver = routeResolver ?? throw new ArgumentNullException(nameof(routeResolver));
        _chatExecutionService = chatExecutionService ?? throw new ArgumentNullException(nameof(chatExecutionService));
    }

    public async Task<StreamingExecutionOutcome> ExecuteStreamingAsync(
        StreamingChatRequest request,
        StreamingBuffer buffer,
        CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        var attemptedRoutes = new HashSet<ModelRouteId>();
        DeclaredQualityTier? lockedTier = null;
        ProviderFailure? lastFailure = null;

        while (true)
        {
            // Create or reuse context with exclusions and locked tier
            var context = lockedTier.HasValue
                ? new RouteResolutionContext(request.BaseRequest, attemptedRoutes.ToHashSet(), lockedTier)
                : RouteResolutionContext.CreateFresh(request.BaseRequest);

            // Resolve route
            var resolution = await _routeResolver.ResolveAsync(context, cancellationToken);

            if (!resolution.IsSuccess)
            {
                // No more routes available
                return StreamingExecutionOutcome.Failure(
                    new ProviderFailure
                    {
                        Category = ProviderFailureCategory.UnknownProviderFailure,
                        Retryability = ProviderFailureRetryability.NotRetryable,
                        Scope = ProviderFailureScope.ModelRoute,
                        SanitizedUpstreamMessage = resolution.FailureValue!.Details ?? "No eligible route"
                    });
            }

            var resolvedRoute = resolution.Route!;

            // Check if we've already attempted this route (safety check)
            if (attemptedRoutes.Contains(resolvedRoute.ModelRouteId))
            {
                return lastFailure != null
                    ? StreamingExecutionOutcome.Failure(lastFailure)
                    : StreamingExecutionOutcome.Failure(
                        new ProviderFailure
                        {
                            Category = ProviderFailureCategory.UnknownProviderFailure,
                            Retryability = ProviderFailureRetryability.NotRetryable,
                            Scope = ProviderFailureScope.ModelRoute,
                            SanitizedUpstreamMessage = "No eligible route for requested model"
                        });
            }

            // Lock the quality tier on first successful resolution
            if (!lockedTier.HasValue)
            {
                lockedTier = resolvedRoute.QualityTier;
            }

            attemptedRoutes.Add(resolvedRoute.ModelRouteId);

            // Reset buffer for new attempt (clear pre-commit state)
            buffer.Clear();

            try
            {
                // Execute against the resolved route
                var outcome = await _chatExecutionService.ExecuteStreamingAsync(
                    request,
                    resolvedRoute,
                    buffer,
                    cancellationToken);

                if (outcome.IsSuccess)
                {
                    // Commit the buffer if we have content
                    if (buffer.GetBufferedChunks().Count > 0)
                    {
                        buffer.Commit();
                    }

                    return StreamingExecutionOutcome.Success(
                        buffer.GetBufferedChunks(),
                        buffer.IsCommitted,
                        resolvedRoute);
                }

                // Streaming failed - check if we should failover
                lastFailure = outcome.FailureValue;
                var failure = outcome.FailureValue;

                if (failure == null)
                {
                    return StreamingExecutionOutcome.Failure(
                        new ProviderFailure
                        {
                            Category = ProviderFailureCategory.UnknownProviderFailure,
                            Retryability = ProviderFailureRetryability.NotRetryable,
                            Scope = ProviderFailureScope.ModelRoute,
                            SanitizedUpstreamMessage = "Unknown execution failure"
                        });
                }

                // Only failover in pre-commit phase (before commit)
                if (!buffer.IsCommitted && failure.Retryability == ProviderFailureRetryability.RetryViaAnotherRoute)
                {
                    // Continue to next attempt - resolver will exclude this route
                    continue;
                }

                // Terminal failure or committed state - do not failover
                return StreamingExecutionOutcome.Failure(failure);
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
