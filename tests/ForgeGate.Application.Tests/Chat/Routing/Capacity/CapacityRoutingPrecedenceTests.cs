using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Capacity;

/// <summary>
/// Tests for cross-policy precedence over capacity.
/// </summary>
public class CapacityRoutingPrecedenceTests
{
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
}
