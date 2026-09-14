using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Health;

/// <summary>
/// Tests for route health ranking and operational eligibility.
/// </summary>
public class RouteHealthRankingTests
{
    [Fact]
    public async Task UnknownHealth_IsEligible()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Healthy_IsEligible()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        health.SetHealth(route.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Degraded_IsEligible()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        health.SetHealth(route.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unavailable_IsFiltered()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        health.SetHealth(route.ModelRoute.ModelRouteId, RouteHealthStatus.Unavailable);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task AllUnavailable_ReturnsNoEligible()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        health.SetHealth(route.ModelRoute.ModelRouteId, RouteHealthStatus.Unavailable);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task Unavailable_DoesNotCallProvider()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        health.SetHealth(route.ModelRoute.ModelRouteId, RouteHealthStatus.Unavailable);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task DegradedPreferredStillBeatsHealthyAcceptable()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var preferred = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptable = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        health.SetHealth(preferred.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        health.SetHealth(acceptable.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { preferred, acceptable } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-pref", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task StaticIneligibilityPrecedesHealth()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var preferredDisabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref"), "provider-model", false, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptableEnabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        health.SetHealth(preferredDisabled.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        health.SetHealth(acceptableEnabled.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { preferredDisabled, acceptableEnabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-acc", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task ToolIncompatibilityPrecedesHealth()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var preferredNoTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptableTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc"), "provider-model", true, ModelCapability.Tools, DeclaredQualityTier.Acceptable) };
        health.SetHealth(preferredNoTools.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        health.SetHealth(acceptableTools.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { preferredNoTools, acceptableTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-acc", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public void HealthProvider_DefaultsToUnknown()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var routeId = ModelRouteId.From("unknown-route");
        Assert.Equal(RouteHealthStatus.Unknown, health.GetHealth(routeId));
    }

    [Fact]
    public void HealthUpdate_IsObservable()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        health.SetHealth(route.ModelRoute.ModelRouteId, RouteHealthStatus.Unavailable);
        Assert.Equal(RouteHealthStatus.Unavailable, health.GetHealth(route.ModelRoute.ModelRouteId));
    }

    [Fact]
    public async Task AvailableRoute_ExecutesOnce()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var available = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-avail"), "provider-model", true, ModelCapability.None) };
        var unavailable = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-unavail"), "provider-model", true, ModelCapability.None) };
        health.SetHealth(unavailable.ModelRoute.ModelRouteId, RouteHealthStatus.Unavailable);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { unavailable, available } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-avail", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task HealthyRoute_OutranksUnknownWithinSameTier()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var healthy = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-healthy"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var unknown = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-unknown"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        health.SetHealth(healthy.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        // unknown stays default (Unknown)
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { healthy, unknown } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-healthy", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task UnknownRoute_OutranksDegradedWithinSameTier()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var unknown = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-unknown"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var degraded = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-degraded"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        // unknown stays default (Unknown)
        health.SetHealth(degraded.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { unknown, degraded } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-unknown", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task HealthyRoute_OutranksDegradedWithinSameTier()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var healthy = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-healthy"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var degraded = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-degraded"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        health.SetHealth(healthy.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        health.SetHealth(degraded.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { healthy, degraded } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-healthy", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task QualityTierOutranksHealthDifference()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var preferredDegraded = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref-degraded"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptableHealthy = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc-healthy"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        health.SetHealth(preferredDegraded.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        health.SetHealth(acceptableHealthy.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { preferredDegraded, acceptableHealthy } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        // Preferred Degraded still wins over Acceptable Healthy
        Assert.Equal("r-pref-degraded", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task QualityTierOutranksHealthLevel()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var preferredUnknown = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref-unknown"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptableHealthy = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc-healthy"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        // preferred stays Unknown, acceptable is Healthy
        health.SetHealth(acceptableHealthy.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { preferredUnknown, acceptableHealthy } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        // Preferred Unknown still wins over Acceptable Healthy
        Assert.Equal("r-pref-unknown", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task SameTierSameHealthy_PreservesConfigOrder()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var a = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-a"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var b = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-b"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        health.SetHealth(a.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        health.SetHealth(b.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { a, b } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        // A comes first in config order
        Assert.Equal("r-a", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task SameTierSameUnknown_PreservesConfigOrder()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var a = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-a"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var b = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-b"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        // Both stay Unknown (default)
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { a, b } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        // A comes first in config order
        Assert.Equal("r-a", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task SameTierSameDegraded_PreservesConfigOrder()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var a = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-a"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var b = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-b"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        health.SetHealth(a.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        health.SetHealth(b.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { a, b } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        // A comes first in config order
        Assert.Equal("r-a", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task UnavailableStillFilteredBeforeRanking()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var unavailable = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-unavail"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var degraded = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-degraded"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        health.SetHealth(unavailable.ModelRoute.ModelRouteId, RouteHealthStatus.Unavailable);
        health.SetHealth(degraded.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { unavailable, degraded } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        // Unavailable filtered, Degraded selected
        Assert.Equal("r-degraded", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task UnavailableRoute_ReturnsNoEligible()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        health.SetHealth(route.ModelRoute.ModelRouteId, RouteHealthStatus.Unavailable);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task PassiveFailureChangesNextSameTierSelection()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var a = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-a"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var b = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-b"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        // Both start Healthy
        health.SetHealth(a.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        health.SetHealth(b.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { a, b } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        
        // First resolution: A selected by config order
        var result1 = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.Equal("r-a", result1.Route!.ModelRouteId.Value);
        
        // Simulate passive failure on A
        health.SetHealth(a.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        
        // Second resolution: B selected because A is now Degraded
        var result2 = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Equal("r-b", result2.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task SuccessRestoresPreferredSameTierRoute()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var a = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-a"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var b = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-b"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        // A starts Degraded, B starts Healthy
        health.SetHealth(a.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        health.SetHealth(b.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { a, b } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        
        // First resolution: B selected (Healthy beats Degraded)
        var result1 = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.Equal("r-b", result1.Route!.ModelRouteId.Value);
        
        // Record success for A (simulating recovery)
        health.SetHealth(a.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        
        // Second resolution: A selected by config order (both Healthy)
        var result2 = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Equal("r-a", result2.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task ProviderExecutesSelectedHealthyRouteExactlyOnce()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var degraded = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-degraded"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var healthy = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-healthy"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        health.SetHealth(degraded.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        health.SetHealth(healthy.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { degraded, healthy } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        // Healthy route selected
        Assert.Equal("r-healthy", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task ToolsStaticEligibilityPrecedesHealth()
    {
        var health = new InMemoryRouteHealthStateProvider();
        // Preferred route without tools but Healthy
        var preferredHealthyNoTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref-healthy-no-tools"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        // Preferred route with tools but Degraded
        var preferredDegradedWithTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-pref-degraded-tools"), "provider-model", true, ModelCapability.Tools, DeclaredQualityTier.Preferred) };
        health.SetHealth(preferredHealthyNoTools.ModelRoute.ModelRouteId, RouteHealthStatus.Healthy);
        health.SetHealth(preferredDegradedWithTools.ModelRoute.ModelRouteId, RouteHealthStatus.Degraded);
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { preferredHealthyNoTools, preferredDegradedWithTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        // Request requires tools
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        // Tool-capable route selected despite Degraded health
        Assert.Equal("r-pref-degraded-tools", result.Route!.ModelRouteId.Value);
    }
}
