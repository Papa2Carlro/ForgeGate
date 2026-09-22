using ForgeGate.Application.AgentGuard;
using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.AgentGuard;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Execution;

/// <summary>
/// Application use-case coordinator for chat completion with transparent route failover.
/// 
/// This orchestrator:
/// 1. Resolves an initial route
/// 2. Executes the request against that route via ChatExecutionService
/// 3. If tool calls are present in the response, evaluates them through Agent Guard
/// 4. If the execution fails with RetryViaAnotherRoute, excludes the failed route
///    and attempts to resolve an alternative route in the SAME quality tier
/// 5. Repeats until success or no more alternatives
/// 
/// Exhaustion contract:
/// - Initial resolution failure -> returns resolution failure directly
/// - All routes in locked tier exhausted -> returns NoEligibleRoute resolution failure
/// - Last provider failure is NOT returned after full exhaustion
/// - Terminal non-retryable provider failure -> returned immediately without failover
/// 
/// It does NOT:
/// - Perform quality tier downgrade
/// - Retrial the same route
/// - Wait/backoff between attempts
/// - Cross logical model fallback
/// - Stream handling
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public sealed class ChatCompletionOrchestrator : IChatCompletionOrchestrator
{
    private readonly IRouteResolver _routeResolver;
    private readonly ChatExecutionService _chatExecutionService;
    private readonly IAgentGuard? _agentGuard;

    public ChatCompletionOrchestrator(
        IRouteResolver routeResolver,
        ChatExecutionService chatExecutionService,
        IAgentGuard? agentGuard = null)
    {
        _routeResolver = routeResolver ?? throw new ArgumentNullException(nameof(routeResolver));
        _chatExecutionService = chatExecutionService ?? throw new ArgumentNullException(nameof(chatExecutionService));
        _agentGuard = agentGuard;
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
                    // Evaluate tool calls through Agent Guard if present
                    var toolCallOutcome = outcome.Response != null
                        ? EvaluateToolCalls(outcome.Response.ToolCalls)
                        : null;
                    if (toolCallOutcome != null)
                    {
                        return toolCallOutcome;
                    }

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

    private ChatCompletionOutcome? EvaluateToolCalls(IReadOnlyList<ToolCallInvocation> toolCalls)
    {
        if (_agentGuard == null || toolCalls.Count == 0)
            return null;

        foreach (var toolCall in toolCalls)
        {
            var action = new AgentAction
            {
                Source = "tool_call",
                RawAction = $"{toolCall.Name} {toolCall.Arguments ?? string.Empty}".Trim(),
                Payload = toolCall.Arguments
            };

            var guardResult = _agentGuard.Evaluate(action);

            if (!guardResult.IsSuccess)
            {
                return ChatCompletionOutcome.Failure(new ProviderFailure
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
                    return ChatCompletionOutcome.Failure(new ProviderFailure
                    {
                        Category = ProviderFailureCategory.AgentGuardDenied,
                        Retryability = ProviderFailureRetryability.NotRetryable,
                        Scope = ProviderFailureScope.Request,
                        SanitizedUpstreamMessage = $"Agent Guard: tool call '{toolCall.Name}' denied by policy",
                        PolicyDecision = PolicyDecision.Deny
                    });
                case PolicyDecision.RequireHumanApproval:
                    return ChatCompletionOutcome.Failure(new ProviderFailure
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
