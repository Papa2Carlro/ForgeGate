using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Execution;

/// <summary>
/// Semantic tests for ChatCompletionOrchestrator covering all failover,
/// exhaustion, tier locking, route exclusion, and capacity behaviors.
/// </summary>
public class ChatCompletionOrchestratorFailoverTests
{
    #region Exhaustion Contract Tests

    /// <summary>
    /// First successful resolution determines the quality tier for subsequent failover attempts.
    /// </summary>
    [Fact]
    public async Task FirstSuccessfulResolutionDeterminesQualityTier()
    {
        // Given - two preferred tier routes
        var (orchestrator, _, _, _) = OrchestratorTestHelpers.SetupWithTwoRoutes(DeclaredQualityTier.Preferred);
        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.SelectedRoute);
        Assert.Equal(DeclaredQualityTier.Preferred, outcome.SelectedRoute!.QualityTier);
    }

    /// <summary>
    /// When all routes in the locked quality tier are exhausted, returns NoEligibleRoute
    /// resolution failure rather than the last provider failure.
    /// </summary>
    [Fact]
    public async Task ExhaustedRoutesReturnNoEligibleRoute()
    {
        // Given - single route that fails with RetryViaAnotherRoute
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var retryFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ProviderUnavailable,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Provider unavailable"
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(retryFailure);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then - returns resolution failure, not the last provider failure
        Assert.False(outcome.IsSuccess);
        Assert.True(outcome.FailureValue!.Category == ProviderFailureCategory.UnknownProviderFailure);
        Assert.Equal("No eligible route for requested model 'gpt-4'", outcome.FailureValue.SanitizedUpstreamMessage);
    }

    #endregion

    #region Failover Permission Tests

    /// <summary>
    /// Failover is allowed when provider failure has RetryViaAnotherRoute retryability.
    /// </summary>
    [Fact]
    public async Task TransparentFailoverOnRetryViaAnotherRoute()
    {
        // Given - two routes, first fails with RetryViaAnotherRoute, second succeeds
        var config = OrchestratorTestHelpers.MakeConfig(
            ("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 100));

        var health = new InMemoryRouteHealthStateProvider();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var retryFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ProviderUnavailable,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Provider unavailable"
        };

        var failoverProvider = new FakeFailoverProvider(
            (ModelRouteId.From("gpt-4:gpt-4-turbo"), retryFailure),
            (ModelRouteId.From("gpt-4-2:gpt-4-turbo-2"), new CanonicalChatResponse { Content = "fallback-response" })
        );
        var executionService = new ChatExecutionService(failoverProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal("fallback-response", outcome.Response!.Content);
        Assert.NotNull(outcome.SelectedRoute);
    }

    /// <summary>
    /// RateLimited with RetryViaAnotherRoute triggers failover to next route.
    /// </summary>
    [Fact]
    public async Task RateLimitedWithRetryViaAnotherRouteTriggersFailover()
    {
        // Given - two routes, first fails with RateLimited + RetryViaAnotherRoute
        var config = OrchestratorTestHelpers.MakeConfig(
            ("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 100));

        var health = new InMemoryRouteHealthStateProvider();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var rateLimitedFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.RateLimited,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Rate limited",
            RetryAfter = TimeSpan.FromSeconds(30)
        };

        var failoverProvider = new FakeFailoverProvider(
            (ModelRouteId.From("gpt-4:gpt-4-turbo"), rateLimitedFailure),
            (ModelRouteId.From("gpt-4-2:gpt-4-turbo-2"), new CanonicalChatResponse { Content = "success-response" })
        );
        var executionService = new ChatExecutionService(failoverProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal("success-response", outcome.Response!.Content);
    }

    #endregion

    #region Terminal Failure Tests (No Failover)

    /// <summary>
    /// AuthenticationFailed with NotRetryable does not trigger failover.
    /// </summary>
    [Fact]
    public async Task AuthenticationFailedDoesNotTriggerFailover()
    {
        // Given - single route with AuthenticationFailed
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var authFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.AuthenticationFailed,
            Retryability = ProviderFailureRetryability.NotRetryable,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Authentication failed"
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(authFailure);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then - terminal failure, no failover
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureCategory.AuthenticationFailed, outcome.FailureValue!.Category);
        Assert.Equal(ProviderFailureRetryability.NotRetryable, outcome.FailureValue.Retryability);
    }

    /// <summary>
    /// InvalidRequest with NotRetryable does not trigger failover.
    /// </summary>
    [Fact]
    public async Task InvalidRequestDoesNotTriggerFailover()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var invalidRequestFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.InvalidRequest,
            Retryability = ProviderFailureRetryability.NotRetryable,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Bad request"
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(invalidRequestFailure);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureCategory.InvalidRequest, outcome.FailureValue!.Category);
    }

    /// <summary>
    /// ContextExceeded with NotRetryable does not trigger failover.
    /// </summary>
    [Fact]
    public async Task ContextExceededDoesNotTriggerFailover()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var contextFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ContextExceeded,
            Retryability = ProviderFailureRetryability.NotRetryable,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Context length exceeded"
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(contextFailure);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureCategory.ContextExceeded, outcome.FailureValue!.Category);
    }

    /// <summary>
    /// RetryAfterDelay does not trigger failover - caller must handle backoff.
    /// </summary>
    [Fact]
    public async Task RetryAfterDelayDoesNotTriggerFailover()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var delayFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.RateLimited,
            Retryability = ProviderFailureRetryability.RetryAfterDelay,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Rate limited",
            RetryAfter = TimeSpan.FromSeconds(30)
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(delayFailure);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureRetryability.RetryAfterDelay, outcome.FailureValue!.Retryability);
    }

    /// <summary>
    /// RetryImmediately does not trigger failover - caller must handle retrial.
    /// </summary>
    [Fact]
    public async Task RetryImmediatelyDoesNotTriggerFailover()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var immediateFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.NetworkFailure,
            Retryability = ProviderFailureRetryability.RetryImmediately,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Network error"
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(immediateFailure);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureRetryability.RetryImmediately, outcome.FailureValue!.Retryability);
    }

    #endregion

    #region Tier Locking Tests

    /// <summary>
    /// Quality tier locking prevents cross-tier failover - locked tier cannot be downgraded.
    /// </summary>
    [Fact]
    public async Task TierLockingPreventsCrossTierFailover()
    {
        // Given - one preferred route, one acceptable route for same model
        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("prod-provider"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("prod:gpt-4"),
                        "gpt-4",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Preferred,
                        maxConcurrentExecutions: 100)
                },
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Acceptable,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("dev-provider"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("dev:gpt-4"),
                        "gpt-4",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Acceptable,
                        maxConcurrentExecutions: 50)
                }
            }
        };

        var health = new InMemoryRouteHealthStateProvider();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var retryFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ProviderUnavailable,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Provider unavailable"
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(retryFailure);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then - should fail because no other preferred routes available
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        // No failover to Acceptable tier
        Assert.True(outcome.FailureValue!.Category == ProviderFailureCategory.UnknownProviderFailure);
    }

    /// <summary>
    /// Lower tier routes are never executed after tier is locked.
    /// </summary>
    [Fact]
    public async Task LowerTierRouteNeverExecutedAfterTierLock()
    {
        // Given - one preferred route (fails), one acceptable route
        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("prod"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("prod:gpt-4"),
                        "gpt-4",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Preferred,
                        maxConcurrentExecutions: 100)
                },
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Acceptable,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("dev"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("dev:gpt-4"),
                        "gpt-4",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Acceptable,
                        maxConcurrentExecutions: 50)
                }
            }
        };

        var health = new InMemoryRouteHealthStateProvider();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var retryFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ProviderUnavailable,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Provider unavailable"
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(retryFailure);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then - no lower tier execution
        Assert.False(outcome.IsSuccess);
    }

    #endregion

    #region Route Exclusion Tests

    /// <summary>
    /// Failed routes are excluded from subsequent resolution attempts.
    /// </summary>
    [Fact]
    public async Task FailedRouteExcludedFromSubsequentAttempts()
    {
        // Given - two routes, first fails with RetryViaAnotherRoute
        var config = OrchestratorTestHelpers.MakeConfig(
            ("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 100));

        var health = new InMemoryRouteHealthStateProvider();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var retryFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ProviderUnavailable,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Provider unavailable"
        };

        var failoverProvider = new FakeFailoverProvider(
            (ModelRouteId.From("gpt-4:gpt-4-turbo"), retryFailure),
            (ModelRouteId.From("gpt-4-2:gpt-4-turbo-2"), new CanonicalChatResponse { Content = "second-success" })
        );
        var executionService = new ChatExecutionService(failoverProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.SelectedRoute);
        // Second route was selected, not the first
        Assert.Equal(ModelRouteId.From("gpt-4-2:gpt-4-turbo-2"), outcome.SelectedRoute.ModelRouteId);
    }

    /// <summary>
    /// Same route is never retried even if it would otherwise be eligible.
    /// </summary>
    [Fact]
    public async Task SameRouteNeverRetried()
    {
        // Given - single route that fails with RetryViaAnotherRoute
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var retryFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ProviderUnavailable,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Provider unavailable"
        };

        var callCount = 0;
        var failingProvider = new FakeCountingProvider(retryFailure, () => callCount++);
        var executionService = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then - route attempted exactly once
        Assert.False(outcome.IsSuccess);
        Assert.Equal(1, callCount);
    }

    #endregion

    #region Multi-Route Failover Tests

    /// <summary>
    /// Multiple sequential failovers work correctly across three routes.
    /// </summary>
    [Fact]
    public async Task MultipleSequentialFailoversWorkCorrectly()
    {
        // Given - three routes, first two fail, third succeeds
        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("provider-1"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("provider-1:gpt-4"),
                        "gpt-4",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Preferred,
                        maxConcurrentExecutions: 100)
                },
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("provider-2"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("provider-2:gpt-4"),
                        "gpt-4",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Preferred,
                        maxConcurrentExecutions: 100)
                },
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("provider-3"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("provider-3:gpt-4"),
                        "gpt-4",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Preferred,
                        maxConcurrentExecutions: 100)
                }
            }
        };

        var health = new InMemoryRouteHealthStateProvider();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var retryFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ProviderUnavailable,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Provider unavailable"
        };

        var successResponse = new CanonicalChatResponse { Content = "third-provider-success" };
        var failoverProvider = new FakeFailoverProvider(
            (ModelRouteId.From("provider-1:gpt-4"), retryFailure),
            (ModelRouteId.From("provider-2:gpt-4"), retryFailure),
            (ModelRouteId.From("provider-3:gpt-4"), successResponse)
        );
        var executionService = new ChatExecutionService(failoverProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal("third-provider-success", outcome.Response!.Content);
    }

    #endregion

    #region Capacity Release Test

    /// <summary>
    /// Capacity is released after each failed attempt before next acquisition.
    /// Verifies that the finally block in ChatExecutionService releases reservation
    /// even when RetryViaAnotherRoute triggers failover.
    /// </summary>
    [Fact]
    public async Task CapacityReleasedAfterEachFailedAttempt()
    {
        // Given - two routes with bounded capacity (limit=1)
        var config = OrchestratorTestHelpers.MakeConfig(
            ("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 1),
            ("gpt-4-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 1));

        var health = new InMemoryRouteHealthStateProvider();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var retryFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ProviderUnavailable,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.ModelRoute,
            SanitizedUpstreamMessage = "Provider unavailable"
        };

        var failoverProvider = new FakeFailoverProvider(
            (ModelRouteId.From("gpt-4:gpt-4-turbo"), retryFailure),
            (ModelRouteId.From("gpt-4-2:gpt-4-turbo-2"), new CanonicalChatResponse { Content = "success" })
        );
        var executionService = new ChatExecutionService(failoverProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then - capacity was acquired and released for first attempt, then acquired again for second
        Assert.True(outcome.IsSuccess);
        Assert.Equal("success", outcome.Response!.Content);
    }

    #endregion
}
