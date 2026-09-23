using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Resolution;

/// <summary>
/// Tests for verifying the resolver uses LogicalModelId as canonical identity,
/// not RequestedModelAlias, for model matching.
/// </summary>
public class RequestedModelDivergenceTests
{
    [Fact]
    public async Task RequestedModelEqualsLogicalModelId_WithDifferentAlias_RouteIsSelected()
    {
        // Arrange: Create a route where RequestedModelAlias differs from LogicalModelId
        var route = new ConfiguredRoute
        {
            RequestedModelAlias = "public-alias", // Different from logical ID
            Enabled = true,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test-provider"),
                LogicalModelId.From("internal-model"), // Canonical ID
                ModelRouteId.From("route1"),
                "provider-native-model", // What provider sees
                true,
                ModelCapability.None)
        };

        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

        // Act: Request using the logical model ID (what clients should use)
        var request = new CanonicalChatRequest
        {
            RequestedModel = "internal-model", // Matches LogicalModelId, not alias
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        // Assert: Route should be selected based on LogicalModelId match
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess, "Route should be selected when RequestedModel matches LogicalModelId");
        Assert.NotNull(result.Route);
        Assert.Equal("internal-model", result.Route!.LogicalModelId.Value);
        Assert.Equal("provider-native-model", result.Route!.ProviderNativeModelId);
        // Note: Cannot assert on RequestedModelAlias as result.Route is ModelRoute
        // Test verifies matching works via LogicalModelId, not alias
    }

    [Fact]
    public async Task RequestedModelDoesNotMatchEither_RouteNotSelected()
    {
        // Arrange: Create a route with specific alias and logical ID
        var route = new ConfiguredRoute
        {
            RequestedModelAlias = "public-alias",
            Enabled = true,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test-provider"),
                LogicalModelId.From("internal-model"),
                ModelRouteId.From("route1"),
                "provider-native-model",
                true,
                ModelCapability.None)
        };

        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

        // Act: Request using a model that matches neither alias nor logical ID
        var request = new CanonicalChatRequest
        {
            RequestedModel = "non-matching-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        // Assert: No route should be selected
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.False(result.IsSuccess, "Route should not be selected when RequestedModel matches neither alias nor LogicalModelId");
        Assert.NotNull(result.FailureValue);
        Assert.Equal(RouteResolutionReason.UnknownRequestedModel, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task MultipleRoutesSameLogicalModelId_DifferentProviders_AllConsidered()
    {
        // Arrange: Multiple routes with same LogicalModelId but different providers/aliases
        var route1 = new ConfiguredRoute
        {
            RequestedModelAlias = "public-alias-1",
            Enabled = true,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("provider-a"),
                LogicalModelId.From("internal-model"), // Same logical ID
                ModelRouteId.From("route-a"),
                "native-model-a",
                true,
                ModelCapability.None)
        };

        var route2 = new ConfiguredRoute
        {
            RequestedModelAlias = "public-alias-2",
            Enabled = true,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("provider-b"),
                LogicalModelId.From("internal-model"), // Same logical ID
                ModelRouteId.From("route-b"),
                "native-model-b",
                true,
                ModelCapability.Tools)
        };

        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route1, route2 } };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

        // Act: Request using the shared logical model ID
        var request = new CanonicalChatRequest
        {
            RequestedModel = "internal-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } },
            ToolRequirement = ToolRequirement.Optional
        };

        // Assert: Both routes should be considered (eligibility will filter based on tools)
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess, "At least one route should be eligible");
        Assert.NotNull(result.Route);
        // Should select the tool-capable route (provider-b) since tools are optional
        Assert.Equal("provider-b", result.Route!.ProviderId.Value);
        Assert.Equal("internal-model", result.Route!.LogicalModelId.Value);
    }

    // Helper to create configured routes with proper values
    private static ConfiguredRoute CreateConfiguredRoute(string alias, ProviderId providerId, LogicalModelId logicalModelId, ModelRouteId modelRouteId, string providerNativeModelId, bool enabled, ModelCapability capabilities)
    {
        return new ConfiguredRoute
        {
            RequestedModelAlias = alias,
            Enabled = enabled,
            ModelRoute = ForgeGate.Domain.Providers.ModelRoute.FromIdsWithOptions(
                providerId,
                logicalModelId,
                modelRouteId,
                providerNativeModelId,
                enabled,
                capabilities)
        };
    }
}