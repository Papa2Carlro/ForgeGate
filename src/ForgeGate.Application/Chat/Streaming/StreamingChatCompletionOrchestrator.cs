using ForgeGate.Application.AgentGuard;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.AgentGuard;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Streaming;

/// <summary>
/// Application use-case coordinator for streaming chat completion with transparent route failover.
/// 
/// This orchestrator:
/// 1. Resolves an initial route (with tier locking)
/// 2. Executes streaming against that route via ChatExecutionService
/// 3. If tool calls are present in the accumulated result, evaluates them through Agent Guard
/// 4. If the execution fails with RetryViaAnotherRoute, excludes the failed route
///    and attempts to resolve an alternative route in the SAME quality tier
/// 5. After commit, no silent failover allowed
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
    private readonly IAgentGuard? _agentGuard;

    public StreamingChatCompletionOrchestrator(
        IRouteResolver routeResolver,
        ChatExecutionService chatExecutionService,
        IAgentGuard? agentGuard = null)
    {
        _routeResolver = routeResolver ?? throw new ArgumentNullException(nameof(routeResolver));
        _chatExecutionService = chatExecutionService ?? throw new ArgumentNullException(nameof(chatExecutionService));
        _agentGuard = agentGuard;
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

                    // Evaluate tool calls through Agent Guard if present
                    var toolCallOutcome = outcome.ToolCalls?.Count > 0
                        ? EvaluateToolCalls(outcome.ToolCalls)
                        : null;
                    if (toolCallOutcome != null)
                    {
                        return toolCallOutcome;
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

    private StreamingExecutionOutcome? EvaluateToolCalls(IReadOnlyList<ToolCallInvocation> toolCalls)
    {
        if (_agentGuard == null || toolCalls.Count == 0)
            return null;

        foreach (var toolCall in toolCalls)
        {
            var action = new AgentAction
            {
                Source = "tool_call",
                RawAction = $"{toolCall.Name}({toolCall.Arguments})",
                Payload = toolCall.Arguments
            };

            var guardResult = _agentGuard.Evaluate(action);

            if (!guardResult.IsSuccess)
            {
                return StreamingExecutionOutcome.Failure(new ProviderFailure
                {
                    Category = ProviderFailureCategory.AgentGuardDenied,
                    Retryability = ProviderFailureRetryability.NotRetryable,
                    Scope = ProviderFailureScope.Request,
                    SanitizedUpstreamMessage = $"Agent Guard pipeline failed: {guardResult.FailureStage} - {guardResult.FailureReason}"
                });
            }

            switch (guardResult.Decision)
            {
                case PolicyDecision.Allow:
                    continue; // All tool calls allowed, proceed
                case PolicyDecision.Deny:
                    return StreamingExecutionOutcome.Failure(new ProviderFailure
                    {
                        Category = ProviderFailureCategory.AgentGuardDenied,
                        Retryability = ProviderFailureRetryability.NotRetryable,
                        Scope = ProviderFailureScope.Request,
                        SanitizedUpstreamMessage = $"Agent Guard: tool call '{toolCall.Name}' denied by policy",
                        PolicyDecision = PolicyDecision.Deny
                    });
                case PolicyDecision.RequireHumanApproval:
                    return StreamingExecutionOutcome.Failure(new ProviderFailure
                    {
                        Category = ProviderFailureCategory.AgentGuardDenied,
                        Retryability = ProviderFailureRetryability.NotRetryable,
                        Scope = ProviderFailureScope.Request,
                        SanitizedUpstreamMessage = $"Agent Guard: tool call '{toolCall.Name}' requires human approval",
                        PolicyDecision = PolicyDecision.RequireHumanApproval
                    });
            }
        }

        return null;
    }
}
