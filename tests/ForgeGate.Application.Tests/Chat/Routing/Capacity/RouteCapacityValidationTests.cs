using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Capacity;

/// <summary>
/// Tests for capacity validation and invariant enforcement.
/// </summary>
public class RouteCapacityValidationTests
{
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
}
