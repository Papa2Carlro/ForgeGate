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
/// Tests for live state changes and race behavior in capacity routing.
/// </summary>
public class CapacityRoutingStateTests
{
    [Fact]
    public async Task ReleaseAffectsNextDecision()
    {
        var routeA = ModelRoute.FromIdsWithOptions(
            ProviderId.From("a"),
            LogicalModelId.From("model"),
            ModelRouteId.From("a:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);
        
        var routeB = ModelRoute.FromIdsWithOptions(
            ProviderId.From("b"),
            LogicalModelId.From("model"),
            ModelRouteId.From("b:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = routeA },
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = routeB }
            }
        };
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result1 = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result1.IsSuccess);
        // Route A is first in config, both have equal capacity, so A is selected
        Assert.Equal("a", result1.Route!.ProviderId.Value);

        // Now hold A's slot
        var hold = await coordinator.TryAcquireAsync(routeA, CancellationToken.None);
        hold!.Reservation!.Dispose();

        // After releasing, A is available again, still first in config
        var result2 = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Equal("a", result2.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task SnapshotReflectsLiveSharedState()
    {
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("test:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 3);

        var snapshot1 = coordinator.GetSnapshot(route);
        Assert.Equal(0, snapshot1.ActiveExecutions);

        var hold = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var snapshot2 = coordinator.GetSnapshot(route);
        Assert.Equal(1, snapshot2.ActiveExecutions);
        Assert.Equal(2, snapshot2.AvailableSlots);

        hold!.Reservation!.Dispose();
        var snapshot3 = coordinator.GetSnapshot(route);
        Assert.Equal(0, snapshot3.ActiveExecutions);
        Assert.Equal(3, snapshot3.AvailableSlots);
    }

    [Fact]
    public async Task SelectionExecutionRaceSafe()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("test:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = route }
            }
        };
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var resolveResult = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(resolveResult.IsSuccess);

        // Simulate race: acquire the route's final slot before execution
        await coordinator.TryAcquireAsync(route, CancellationToken.None);

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome.FailureValue!.Category);
        Assert.Empty(mockProvider.CapturedRequests);
    }

    [Fact]
    public async Task ResolverNeverAcquiresCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("test:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = route }
            }
        };
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        await resolver.ResolveAsync(request, CancellationToken.None);
        await resolver.ResolveAsync(request, CancellationToken.None);
        await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task NoCapacityAvailableHasControlledApiResponse()
    {
        var preferredRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("preferred"),
            LogicalModelId.From("model"),
            ModelRouteId.From("preferred:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);
        
        var acceptableRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("acceptable"),
            LogicalModelId.From("model"),
            ModelRouteId.From("acceptable:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 5);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = preferredRoute },
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = acceptableRoute }
            }
        };
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);

        await coordinator.TryAcquireAsync(preferredRoute, CancellationToken.None);
        await coordinator.TryAcquireAsync(preferredRoute, CancellationToken.None);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoCapacityAvailable, result.FailureValue!.Reason);
        Assert.NotNull(result.FailureValue.Details);
        Assert.Contains("model", result.FailureValue.Details);
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
