using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Eligibility;

/// <summary>
/// Tests for hard route eligibility based on tool capabilities.
/// </summary>
public class HardRouteEligibilityTests
{
    [Fact]
    public async Task NoToolsRequired_PlainRequest_MakesNonToolsRouteEligible()
    {
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.None };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ToolsRequired_NonToolsRouteFiltered()
    {
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task ToolCapableRoute_SelectedWhenToolsRequired()
    {
        var noTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var withTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", true, ModelCapability.Tools) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { noTools, withTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r2", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task NoToolCapableRoute_ReturnsNoEligible()
    {
        var noTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { noTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task NoEligibleToolRoute_DoesNotCallProvider()
    {
        var noTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { noTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task SelectedToolCapableRoute_ExecutesOnce()
    {
        var withTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.Tools) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { withTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r1", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task PlainRequest_DoesNotRequireTools()
    {
        var noTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { noTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.None };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ToolChoiceNone_PlainRequest_Eligible()
    {
        // ToolChoice = "none" maps to ToolRequirement.None; non-tools route remains eligible
        var noTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { noTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.None };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ToolChoiceAuto_RequiresTools()
    {
        var noTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var withTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", true, ModelCapability.Tools) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { noTools, withTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r2", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task ExplicitRequiredToolChoice_RequiresTools()
    {
        var withTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.Tools) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { withTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Required };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }
}
