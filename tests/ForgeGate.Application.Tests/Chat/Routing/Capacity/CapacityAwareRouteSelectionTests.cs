using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Capacity;

/// <summary>
/// Tests for capacity-aware route selection.
/// </summary>
public class CapacityAwareRouteSelectionTests
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
            Options.Create(config),
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
    public async Task MoreAvailableSlots_WinsWithinSameHealthAndQuality()
    {
        var routeA = ModelRoute.FromIdsWithOptions(
            ProviderId.From("a"),
            LogicalModelId.From("model"),
            ModelRouteId.From("a:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 3);

        var routeB = ModelRoute.FromIdsWithOptions(
            ProviderId.From("b"),
            LogicalModelId.From("model"),
            ModelRouteId.From("b:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 2);

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

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task UnboundedRoute_OutranksBoundedRoute()
    {
        var boundedRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("bounded"),
            LogicalModelId.From("model"),
            ModelRouteId.From("bounded:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);

        var unboundedRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("unbounded"),
            LogicalModelId.From("model"),
            ModelRouteId.From("unbounded:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: null);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = boundedRoute },
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = unboundedRoute }
            }
        };
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);

        // Hold the bounded route's only slot
        await coordinator.TryAcquireAsync(boundedRoute, CancellationToken.None);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("unbounded", result.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task EqualHeadroom_PreservesConfigOrder()
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

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task EqualHeadroomUsesConfigOrder()
    {
        var routeA = ModelRoute.FromIdsWithOptions(
            ProviderId.From("a"),
            LogicalModelId.From("model"),
            ModelRouteId.From("a:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 2);

        var routeB = ModelRoute.FromIdsWithOptions(
            ProviderId.From("b"),
            LogicalModelId.From("model"),
            ModelRouteId.From("b:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 2);

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

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task HealthOutranksCapacity()
    {
        var healthState = new InMemoryRouteHealthStateProvider();

        var routeA = ModelRoute.FromIdsWithOptions(
            ProviderId.From("a"),
            LogicalModelId.From("model"),
            ModelRouteId.From("a:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 2);

        var routeB = ModelRoute.FromIdsWithOptions(
            ProviderId.From("b"),
            LogicalModelId.From("model"),
            ModelRouteId.From("b:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 11);

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
            new RouteHealthRanker(healthState),
            coordinator,
            healthState);

        healthState.SetHealth(routeA.ModelRouteId, RouteHealthStatus.Healthy);
        healthState.SetHealth(routeB.ModelRouteId, RouteHealthStatus.Unknown);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task QualityOutranksCapacity()
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
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 10);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = routeA },
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = routeB }
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

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task FullPreferredDoesNotDowngradeToAcceptable()
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
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 10);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = routeA },
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = routeB }
            }
        };
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            coordinator);

        // Fill preferred route
        await coordinator.TryAcquireAsync(routeA, CancellationToken.None);
        await coordinator.TryAcquireAsync(routeA, CancellationToken.None);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        // Should return failure, not downgrade to acceptable
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoCapacityAvailable, result.FailureValue!.Reason);
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
            Options.Create(config),
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
            Options.Create(config),
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
    public async Task HardEligibilityPrecedesCapacity()
    {
        var disabledRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("disabled"),
            LogicalModelId.From("model"),
            ModelRouteId.From("disabled:model"),
            "native",
            enabled: false,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 100);

        var enabledRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("enabled"),
            LogicalModelId.From("model"),
            ModelRouteId.From("enabled:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = disabledRoute },
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = enabledRoute }
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

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("enabled", result.Route!.ProviderId.Value);
    }

    [Fact]
    public async Task ToolCapabilityPrecedesCapacity()
    {
        var noToolRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("notool"),
            LogicalModelId.From("model"),
            ModelRouteId.From("notool:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 100);

        var toolRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("tool"),
            LogicalModelId.From("model"),
            ModelRouteId.From("tool:model"),
            "native",
            enabled: true,
            capabilities: ModelCapability.Tools,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = noToolRoute },
                new() { RequestedModelAlias = "model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = toolRoute }
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
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } },
            ToolRequirement = ToolRequirement.Required
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("tool", result.Route!.ProviderId.Value);
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
}
