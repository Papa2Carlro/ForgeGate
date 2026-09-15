using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.Routing.Capacity;

/// <summary>
/// Tests for route capacity acquisition semantics.
/// </summary>
public class RouteCapacityAcquisitionTests
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
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

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
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

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
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

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
}
