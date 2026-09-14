using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Capacity;

/// <summary>
/// Tests for the route capacity coordinator.
/// </summary>
public class CapacityCoordinatorTests
{
    [Fact]
    public async Task UnboundedRoute_AcquiresAndReleases()
    {
        var route = ModelRoute.FromIds(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"));
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        Assert.Single(mockProvider.CapturedRequests);
    }

    [Fact]
    public async Task BoundedRoute_FirstSlotAcquires()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        // Capacity released after execution completes
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task BoundedRoute_RejectsWhenFull()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome1 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome1.IsSuccess);

        // Hold a reservation to make the route full
        var acquireResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        Assert.NotNull(reservation);
        Assert.Equal(1, coordinator.GetActiveCount(route.ModelRouteId));

        var outcome2 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome2.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome2.FailureValue!.Category);
        Assert.Single(mockProvider.CapturedRequests);
        reservation!.Dispose();
    }

    [Fact]
    public async Task ReleaseRestoresCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome1 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome1.IsSuccess);
        // After first execution completes, capacity is released
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));

        // Now hold a slot and verify second execution is rejected
        var acquireResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        Assert.NotNull(reservation);
        Assert.Equal(1, coordinator.GetActiveCount(route.ModelRouteId));

        var outcome2 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome2.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome2.FailureValue!.Category);
        Assert.Single(mockProvider.CapturedRequests);
        reservation!.Dispose();
    }

    [Fact]
    public async Task ReleaseIsIdempotent()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome1 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome1.IsSuccess);

        var outcome2 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome2.IsSuccess);
    }

    [Fact]
    public async Task SeparateRoutes_HaveIndependentCapacity()
    {
        var routeA = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test-a"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r-a"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var routeB = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test-b"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r-b"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        // Hold slots on both routes concurrently
        var holdA = await coordinator.TryAcquireAsync(routeA, CancellationToken.None);
        var holdB = await coordinator.TryAcquireAsync(routeB, CancellationToken.None);
        Assert.NotNull(holdA);
        Assert.NotNull(holdB);
        Assert.Equal(1, coordinator.GetActiveCount(routeA.ModelRouteId));
        Assert.Equal(1, coordinator.GetActiveCount(routeB.ModelRouteId));

        // Release and verify
        holdA!.Reservation!.Dispose();
        holdB!.Reservation!.Dispose();
        Assert.Equal(0, coordinator.GetActiveCount(routeA.ModelRouteId));
        Assert.Equal(0, coordinator.GetActiveCount(routeB.ModelRouteId));
    }

    [Fact]
    public async Task ProviderNotCalledWhenAtCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = result.Reservation;
        Assert.NotNull(reservation);

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome.FailureValue!.Category);
        Assert.Empty(mockProvider.CapturedRequests);
        reservation!.Dispose();
    }

    [Fact]
    public async Task LocalCapacityFailure_HasCorrectCategory()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var acquireResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome.FailureValue!.Category);
        reservation!.Dispose();
    }

    [Fact]
    public async Task LocalCapacityFailure_DoesNotChangeHealth()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var healthProvider = new InMemoryRouteHealthStateProvider();
        healthProvider.SetHealth(route.ModelRouteId, RouteHealthStatus.Healthy);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(healthProvider), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var acquireResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
        reservation!.Dispose();
    }

    [Fact]
    public async Task SuccessfulExecution_ReleasesCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task ExpectedFailure_ReleasesCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryAfterDelay,
                Scope = ProviderFailureScope.ModelRoute
            });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task CallerCancellation_ReleasesCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeCancellingProvider();
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();
        // TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAsync<System.Threading.Tasks.TaskCanceledException>(
            async () => await service.ExecuteAsync(request, route, cts.Token));
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task UnexpectedException_ReleasesCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeThrowingProvider();
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.ExecuteAsync(request, route, CancellationToken.None));
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task ConcurrentAcquisition_NeverExceedsLimit()
    {
        // Direct reservation-based proof: hold exactly 2, prove no more can be acquired
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 2);
        var coordinator = new InMemoryRouteCapacityCoordinator();

        // Acquire and HOLD exactly 2 reservations
        var hold1 = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var hold2 = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        Assert.NotNull(hold1);
        Assert.NotNull(hold2);
        Assert.Equal(2, coordinator.GetActiveCount(route.ModelRouteId));

        // While both are held, third acquisition must fail
        var thirdResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        Assert.Equal(RouteCapacityAcquireStatus.AtCapacity, thirdResult.Status);

        // Cleanup
        hold1!.Reservation!.Dispose();
        hold2!.Reservation!.Dispose();
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task ResolverBehavior_Unchanged()
    {
        // routeA has limit=1 and we hold its only slot - resolver should still select it
        var routeA = ModelRoute.FromIdsWithOptions(
            ProviderId.From("p-a"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r-a"),
            "native-a",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);
        var routeB = ModelRoute.FromIdsWithOptions(
            ProviderId.From("p-b"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r-b"),
            "native-b",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);

        var coordinator = new InMemoryRouteCapacityCoordinator();
        // Hold routeA's only slot
        var heldReservation = await coordinator.TryAcquireAsync(routeA, CancellationToken.None);
        Assert.NotNull(heldReservation);
        Assert.Equal(1, coordinator.GetActiveCount(routeA.ModelRouteId));

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new ConfiguredRoute
                {
                    RequestedModelAlias = "model",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = routeA
                },
                new ConfiguredRoute
                {
                    RequestedModelAlias = "model",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = routeB
                }
            }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        // Resolver should still select routeA (capacity is not considered)
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-a", result.Route!.ModelRouteId.Value);

        // Cleanup
        heldReservation!.Reservation!.Dispose();
    }

    [Fact]
    public void ZeroConcurrencyLimit_Rejected()
    {
        // Zero is invalid - should throw at construction time
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Acceptable,
                maxConcurrentExecutions: 0));
    }

    [Fact]
    public void NegativeConcurrencyLimit_Rejected()
    {
        // Negative is invalid - should throw at construction time
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Acceptable,
                maxConcurrentExecutions: -1));
    }

    [Fact]
    public async Task ConfigurationPropagation()
    {
        // Prove that ConfiguredRoute.MaxConcurrentExecutions propagates to ModelRoute
        var configuredRoute = new ConfiguredRoute
        {
            RequestedModelAlias = "model",
            Enabled = true,
            QualityTier = DeclaredQualityTier.Preferred,
            MaxConcurrentExecutions = 3,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Preferred)
        };

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute> { configuredRoute }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Route);
        Assert.Equal(3, result.Route!.MaxConcurrentExecutions);
    }

    [Fact]
    public async Task ConfiguredZeroLimit_Rejected()
    {
        // Zero is invalid - should throw during resolution
        var configuredRoute = new ConfiguredRoute
        {
            RequestedModelAlias = "model",
            Enabled = true,
            QualityTier = DeclaredQualityTier.Preferred,
            MaxConcurrentExecutions = 0,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Preferred)
        };

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute> { configuredRoute }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ResolveAsync(request, CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task ConfiguredNegativeLimit_Rejected()
    {
        // Negative is invalid - should throw during resolution
        var configuredRoute = new ConfiguredRoute
        {
            RequestedModelAlias = "model",
            Enabled = true,
            QualityTier = DeclaredQualityTier.Preferred,
            MaxConcurrentExecutions = -1,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Preferred)
        };

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute> { configuredRoute }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ResolveAsync(request, CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task Coordinator_DefensivelyRejectsInvalidLimit()
    {
        // Construct an invalid ModelRoute via 'with' expression (bypasses factory validation)
        // This tests the coordinator's defensive behavior against invalid states
        var invalidRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1) with
        { MaxConcurrentExecutions = 0 };

        var coordinator = new InMemoryRouteCapacityCoordinator();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => coordinator.TryAcquireAsync(invalidRoute, CancellationToken.None));
    }

    [Fact]
    public async Task ConfiguredNullLimit_RemainsUnbounded()
    {
        // Null should propagate as unbounded
        var configuredRoute = new ConfiguredRoute
        {
            RequestedModelAlias = "model",
            Enabled = true,
            QualityTier = DeclaredQualityTier.Preferred,
            MaxConcurrentExecutions = null,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Preferred)
        };

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute> { configuredRoute }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Route);
        Assert.Null(result.Route!.MaxConcurrentExecutions);
    }

    [Fact]
    public async Task DIReadAndAcquireShareSameCoordinatorInstance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRouteCapacityCoordinator, InMemoryRouteCapacityCoordinator>();
        services.AddSingleton<IRouteCapacityStateProvider>(sp => sp.GetRequiredService<IRouteCapacityCoordinator>() as IRouteCapacityStateProvider
            ?? throw new InvalidOperationException("Coordinator does not implement state provider"));

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IRouteCapacityCoordinator>();
        var stateProvider = provider.GetRequiredService<IRouteCapacityStateProvider>();

        Assert.Same(coordinator, stateProvider);
    }
}
