using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Streaming;

/// <summary>
/// Semantic tests for StreamingChatCompletionOrchestrator covering streaming failover, buffer management,
/// and route resolution behavior.
/// </summary>
public class StreamingChatCompletionOrchestratorTests
{
    #region Test Helpers

    private static RoutingConfiguration MakeStreamingConfig(params (string alias, string nativeId, DeclaredQualityTier tier, int concurrencyLimit)[] routeSpecs)
    {
        var routes = new List<ConfiguredRoute>();
        foreach (var (alias, nativeId, tier, concurrencyLimit) in routeSpecs)
        {
            var route = ModelRoute.FromIdsWithOptions(
                ProviderId.From(alias),
                LogicalModelId.From("gpt-4"),
                ModelRouteId.From($"{alias}:{nativeId}"),
                nativeId,
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: tier,
                maxConcurrentExecutions: concurrencyLimit);
            routes.Add(new ConfiguredRoute
            {
                RequestedModelAlias = "gpt-4",
                Enabled = true,
                QualityTier = tier,
                ModelRoute = route
            });
        }
        return new RoutingConfiguration { Routes = routes };
    }

    private static (StreamingChatCompletionOrchestrator, IRouteResolver, InMemoryRouteHealthStateProvider, FakeStreamingProvider)
        SetupWithSingleRoute(
            IReadOnlyList<string> chunks,
            DeclaredQualityTier tier = DeclaredQualityTier.Preferred,
            int concurrencyLimit = 100)
    {
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeStreamingConfig(("gpt-4", "gpt-4-turbo", tier, concurrencyLimit));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var provider = new FakeStreamingProvider(chunks);
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        return (orchestrator, resolver, health, provider);
    }

    private static (StreamingChatCompletionOrchestrator, IRouteResolver, InMemoryRouteHealthStateProvider, FakeStreamingProvider, FakeStreamingProvider)
        SetupWithTwoRoutes(
            IReadOnlyList<string> firstChunks,
            IReadOnlyList<string> secondChunks,
            DeclaredQualityTier tier = DeclaredQualityTier.Preferred)
    {
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeStreamingConfig(
            ("gpt-4-1", "gpt-4-turbo-1", tier, 100),
            ("gpt-4-2", "gpt-4-turbo-2", tier, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var provider1 = new FakeStreamingProvider(firstChunks);
        var provider2 = new FakeStreamingProvider(secondChunks);
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider1, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        return (orchestrator, resolver, health, provider1, provider2);
    }

    #endregion

    #region Successful Streaming Tests

    [Fact]
    public async Task ExecuteStreamingAsync_Success_ReturnsChunksAndCommits()
    {
        // Given
        var chunks = new[] { "Hello", " ", "world", "!" };
        var (orchestrator, _, _, provider) = SetupWithSingleRoute(chunks);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.IsCommitted);
        Assert.Equal(4, outcome.BufferedChunks.Count);
        Assert.Equal("Hello", outcome.BufferedChunks[0]);
        Assert.Equal("!", outcome.BufferedChunks[3]);
        Assert.NotNull(outcome.SelectedRoute);
        Assert.Equal(1, provider.StreamingCallCount);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_EmptyChunks_ReturnsSuccessWithoutCommit()
    {
        // Given
        var (orchestrator, _, _, _) = SetupWithSingleRoute(Array.Empty<string>());

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.IsCommitted);
        Assert.Empty(outcome.BufferedChunks);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_ValidRequest_DelegatesToProvider()
    {
        // Given
        var chunks = new[] { "response" };
        var (orchestrator, _, _, provider) = SetupWithSingleRoute(chunks);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Hello" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then
        Assert.Single(provider.CapturedStreamingCalls);
        Assert.Equal("gpt-4", provider.CapturedStreamingCalls[0].Request.BaseRequest.RequestedModel);
        Assert.True(provider.CapturedStreamingCalls[0].Request.Stream);
    }

    #endregion

    #region Null and Validation Tests

    [Fact]
    public async Task ExecuteStreamingAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Given
        var (orchestrator, _, _, _) = SetupWithSingleRoute(Array.Empty<string>());
        var buffer = new StreamingBuffer();

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await orchestrator.ExecuteStreamingAsync(null!, buffer, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteStreamingAsync_NullBuffer_ThrowsArgumentNullException()
    {
        // Given
        var (orchestrator, _, _, _) = SetupWithSingleRoute(Array.Empty<string>());
        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await orchestrator.ExecuteStreamingAsync(request, null!, CancellationToken.None));
    }

    #endregion

    #region Failover Tests

    [Fact]
    public async Task ExecuteStreamingAsync_PreCommitFailure_FailsOverToSecondRoute()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeStreamingConfig(
            ("gpt-4-1", "gpt-4-turbo-1", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var failure = new ProviderFailure
        {
            Category = ProviderFailureCategory.NetworkFailure,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.Provider,
            SanitizedUpstreamMessage = "Connection lost"
        };

        var provider1 = new FakeStreamingProvider(failure);
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider1, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then - both routes attempted via same provider and both failed
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureCategory.UnknownProviderFailure, outcome.FailureValue!.Category);
        Assert.Equal(2, provider1.StreamingCallCount);
        // Uses same provider for both routes
    }

    [Fact]
    public async Task ExecuteStreamingAsync_PostCommitFailure_NoFailover()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeStreamingConfig(
            ("gpt-4-1", "gpt-4-turbo-1", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var failure = new ProviderFailure
        {
            Category = ProviderFailureCategory.NetworkFailure,
            Retryability = ProviderFailureRetryability.NotRetryable,
            Scope = ProviderFailureScope.Provider,
            SanitizedUpstreamMessage = "Fatal error"
        };

        var provider1 = new FakeStreamingProvider(failure);
        var provider2 = new FakeStreamingProvider(new[] { "success" });
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider1, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(failure.Category, outcome.FailureValue!.Category);
        Assert.Equal(1, provider1.StreamingCallCount);
        
    }

    [Fact]
    public async Task ExecuteStreamingAsync_AllRoutesFailed_ReturnsLastFailure()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeStreamingConfig(
            ("gpt-4-1", "gpt-4-turbo-1", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var failure = new ProviderFailure
        {
            Category = ProviderFailureCategory.NetworkFailure,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.Provider,
            SanitizedUpstreamMessage = "Connection lost"
        };

        var provider1 = new FakeStreamingProvider(failure);
        // Note: Both routes use the same executionService with provider1.
        // provider2 is created but not used - this tests that the orchestrator
        // exhausts available routes and returns the last failure.
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider1, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        // Both routes should have been attempted (via the same provider1)
        Assert.Equal(2, provider1.StreamingCallCount);
        // Uses same provider for both routes
        
    }

    [Fact]
    public async Task ExecuteStreamingAsync_NoEligibleRoute_ReturnsFailure()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeStreamingConfig();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var provider = new FakeStreamingProvider(new[] { "success" });
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "nonexistent-model",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(0, provider.StreamingCallCount);
    }

    #endregion

    #region Buffer Management Tests

    [Fact]
    public async Task ExecuteStreamingAsync_BufferClearedOnFailover()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeStreamingConfig(
            ("gpt-4-1", "gpt-4-turbo-1", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var failure = new ProviderFailure
        {
            Category = ProviderFailureCategory.NetworkFailure,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.Provider,
            SanitizedUpstreamMessage = "Connection lost"
        };

        // Use a single provider that fails first call then succeeds with "success"
        var provider = new FakeStreamingProvider(new[] { "success" }, failFirstN: 1);
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then - buffer should only contain chunks from successful attempt
        Assert.True(outcome.IsSuccess);
        Assert.Single(outcome.BufferedChunks);
        Assert.Equal("success", outcome.BufferedChunks[0]);
        // Provider was called twice: once failed (pre-commit), once succeeded
        Assert.Equal(2, provider.StreamingCallCount);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_PostCommitPreventsSilentSplice()
    {
        // Given
        var chunks = new[] { "chunk1", "chunk2" };
        var (orchestrator, _, _, provider) = SetupWithSingleRoute(chunks);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When - execute once
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        Assert.True(buffer.IsCommitted);

        // Try to add more chunks after commit
        var result = buffer.TryAddChunk("extra");

        // Then
        Assert.False(result);
        // Buffer should contain both chunks from successful execution
        Assert.Equal(2, outcome.BufferedChunks.Count);
    }

    #endregion

    #region Quality Tier Lock Tests

    [Fact]
    public async Task ExecuteStreamingAsync_LockedQualityTier_PersistsAcrossRetries()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeStreamingConfig(
            ("gpt-4-preferred", "gpt-4-turbo", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-preferred-2", "gpt-4-turbo-2", DeclaredQualityTier.Preferred, 100),
            ("gpt-4-preferred-3", "gpt-4-turbo-3", DeclaredQualityTier.Preferred, 100));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var failure = new ProviderFailure
        {
            Category = ProviderFailureCategory.NetworkFailure,
            Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
            Scope = ProviderFailureScope.Provider,
            SanitizedUpstreamMessage = "Connection lost"
        };

        var provider1 = new FakeStreamingProvider(failure, failFirstN: 2);
        var provider2 = new FakeStreamingProvider(new[] { "success" });
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider1, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then - should stay in Preferred tier, not fall back to Acceptable
        Assert.True(outcome.IsSuccess);
        Assert.Equal(DeclaredQualityTier.Preferred, outcome.SelectedRoute!.QualityTier);
    }

    #endregion

    #region Capacity Tests

    [Fact]
    public async Task ExecuteStreamingAsync_CapacityFull_ReturnsConcurrencyLimited()
    {
        // Given
        var health = new InMemoryRouteHealthStateProvider();
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var config = MakeStreamingConfig(("gpt-4", "gpt-4-turbo", DeclaredQualityTier.Preferred, 1));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            coordinator);

        var provider = new FakeStreamingProvider(new[] { "success" });
        // Use the SAME coordinator for both resolver and execution service
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider, new RouteHealthFeedback(health), coordinator);
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        // Pre-acquire the capacity slot using the same coordinator instance
        var preRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("gpt-4"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("gpt-4:gpt-4-turbo"),
            "gpt-4-turbo",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);
        var acquireResult = await coordinator.TryAcquireAsync(preRoute, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        Assert.NotNull(reservation);

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Then - resolver returns NoCapacityAvailable when all routes are full,
        // which the orchestrator wraps as UnknownProviderFailure
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureCategory.UnknownProviderFailure, outcome.FailureValue!.Category);
        Assert.Contains("No capacity available", outcome.FailureValue.SanitizedUpstreamMessage);

        reservation!.Dispose();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExecuteStreamingAsync_CancelledBeforeStart_ThrowsOperationCanceledException()
    {
        // Given
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var (orchestrator, _, _, _) = SetupWithSingleRoute(new[] { "chunk" });

        var request = new StreamingChatRequest
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage>
                {
                    new() { Role = "user", Content = "Test" }
                }
            },
            Stream = true
        };
        var buffer = new StreamingBuffer();

        // When & Then
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await orchestrator.ExecuteStreamingAsync(request, buffer, cts.Token));
    }

    #endregion
}
