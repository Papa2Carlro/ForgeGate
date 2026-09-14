using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.Routing.Capacity;

/// <summary>
/// Tests for route capacity reservation and release lifecycle.
/// </summary>
public class RouteCapacityReservationTests
{
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
}
