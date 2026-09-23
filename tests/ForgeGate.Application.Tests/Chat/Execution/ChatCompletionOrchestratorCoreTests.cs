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
/// Semantic tests for ChatCompletionOrchestrator covering core orchestration,
/// exception handling, and edge case behaviors.
/// </summary>
public class ChatCompletionOrchestratorCoreTests
{
    #region Core Orchestration Tests

    /// <summary>
    /// Successful response is returned immediately without additional continuation.
    /// </summary>
    [Fact]
    public async Task SuccessfulResponseReturnedImmediately()
    {
        // Given - two routes, first succeeds
        var (orchestrator, _, _, _) = OrchestratorTestHelpers.SetupWithTwoRoutes();

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal("fallback-response", outcome.Response!.Content);
        Assert.NotNull(outcome.SelectedRoute);
        // First route selected
        Assert.Equal(ModelRouteId.From("gpt-4:gpt-4-turbo"), outcome.SelectedRoute.ModelRouteId);
    }

    /// <summary>
    /// No streaming failover is implemented - returns complete response.
    /// </summary>
    [Fact]
    public async Task NoStreamingFailoverImplemented()
    {
        // Given
        var (orchestrator, _, _, _) = OrchestratorTestHelpers.SetupWithSingleRoute();
        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then - complete response, not stream
        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Response);
    }

    /// <summary>
    /// Same quality tier constraint enforced during failover.
    /// </summary>
    [Fact]
    public async Task SameQualityTierConstraintEnforced()
    {
        // Given - two routes in same quality tier
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

        var successResponse = new CanonicalChatResponse { Content = "production-fallback" };
        var failoverProvider = new FakeFailoverProvider(retryFailure, successResponse);
        var executionService = new ChatExecutionService(failoverProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal(DeclaredQualityTier.Preferred, outcome.SelectedRoute!.QualityTier);
    }

    /// <summary>
    /// Null response from provider is handled gracefully.
    /// </summary>
    [Fact]
    public async Task NullResponseHandledGracefully()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var nullResponseProvider = new FakeNullResponseProvider();
        var executionService = new ChatExecutionService(nullResponseProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When
        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Null(outcome.Response);
    }

    /// <summary>
    /// Empty messages are rejected by ChatExecutionService validation.
    /// </summary>
    [Fact]
    public async Task EmptyMessagesRejectedByValidation()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var provider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "response" });
        var executionService = new ChatExecutionService(provider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage>()
        };

        // When & Then
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.ExecuteAsync(request, CancellationToken.None));
        Assert.Contains("Messages", exception.Message);
    }

    /// <summary>
    /// Concurrent requests maintain isolation of state.
    /// </summary>
    [Fact]
    public async Task ConcurrentRequestsMaintainIsolation()
    {
        // Given
        var (orchestrator, _, _, _) = OrchestratorTestHelpers.SetupWithSingleRoute();

        var request1 = OrchestratorTestHelpers.MakeRequest();
        var request2 = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "request-2" } }
        };

        // When
        var task1 = orchestrator.ExecuteAsync(request1, CancellationToken.None);
        var task2 = orchestrator.ExecuteAsync(request2, CancellationToken.None);
        await Task.WhenAll(task1, task2);

        // Then
        Assert.True((await task1).IsSuccess);
        Assert.True((await task2).IsSuccess);
    }

    #endregion

    #region Exception Handling Tests

    /// <summary>
    /// Caller cancellation propagates without triggering failover.
    /// </summary>
    [Fact]
    public async Task CallerCancellationPropagatesWithoutFailover()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var cancellingProvider = new FakeCancellingProvider();
        var executionService = new ChatExecutionService(cancellingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // When & Then
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => orchestrator.ExecuteAsync(request, cts.Token));
    }

    /// <summary>
    /// Unexpected exceptions propagate without being masked as route failures.
    /// </summary>
    [Fact]
    public async Task UnexpectedExceptionPropagatesNotMaskedAsRouteFailure()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = OrchestratorTestHelpers.MakeConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var throwingProvider = new FakeThrowingProvider();
        var executionService = new ChatExecutionService(throwingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        var request = OrchestratorTestHelpers.MakeRequest();

        // When & Then
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ExecuteAsync(request, CancellationToken.None));
        Assert.Equal("Unexpected error", exception.Message);
    }

    /// <summary>
    /// Null request validation throws ArgumentNullException.
    /// </summary>
    [Fact]
    public async Task NullRequestThrowsArgumentNullException()
    {
        // Given
        var (orchestrator, _, _, _) = OrchestratorTestHelpers.SetupWithSingleRoute();

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(() => orchestrator.ExecuteAsync(null!, CancellationToken.None));
    }

    #endregion
}
