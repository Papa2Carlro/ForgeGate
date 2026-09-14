using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Tests.Chat.Routing.Capacity;

/// <summary>
/// Tests for capacity snapshot state and availability filtering.
/// </summary>
public class CapacityAvailabilityTests
{
    [Fact]
    public async Task UnboundedRouteSnapshot_IsAvailable()
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
            maxConcurrentExecutions: null);

        var snapshot = coordinator.GetSnapshot(route);

        Assert.False(snapshot.IsBounded);
        Assert.Null(snapshot.MaxConcurrentExecutions);
        Assert.Equal(0, snapshot.ActiveExecutions);
        Assert.Null(snapshot.AvailableSlots);
        Assert.False(snapshot.IsAtCapacity);
    }

    [Fact]
    public async Task BoundedFreeRouteSnapshot_ReturnsCorrectState()
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
            maxConcurrentExecutions: 2);

        await coordinator.TryAcquireAsync(route, CancellationToken.None);

        var snapshot = coordinator.GetSnapshot(route);

        Assert.True(snapshot.IsBounded);
        Assert.Equal(2, snapshot.MaxConcurrentExecutions);
        Assert.Equal(1, snapshot.ActiveExecutions);
        Assert.Equal(1, snapshot.AvailableSlots);
        Assert.False(snapshot.IsAtCapacity);
    }

    [Fact]
    public async Task FullRouteFilteredWithinSameTier()
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
            Microsoft.Extensions.Options.Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);

        // Fill routeA's only slot
        await coordinator.TryAcquireAsync(routeA, CancellationToken.None);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("b", result.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task AllRoutesInSelectedTierFull_ReturnsNoCapacity()
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
            Microsoft.Extensions.Options.Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);

        await coordinator.TryAcquireAsync(routeA, CancellationToken.None);
        await coordinator.TryAcquireAsync(routeB, CancellationToken.None);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoCapacityAvailable, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task NoCapacityAvailableDoesNotCallProvider()
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
            Microsoft.Extensions.Options.Options.Create(config),
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
    }

    [Fact]
    public async Task LowerTierCapacityIrrelevantWhenSelectedTierHasCapacity()
    {
        var preferredRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("preferred"),
            LogicalModelId.From("model"),
            ModelRouteId.From("preferred:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 2);
        
        var acceptableRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("acceptable"),
            LogicalModelId.From("model"),
            ModelRouteId.From("acceptable:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: null);

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
            Microsoft.Extensions.Options.Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("preferred", result.Route!.ProviderId.Value);
    }
}
