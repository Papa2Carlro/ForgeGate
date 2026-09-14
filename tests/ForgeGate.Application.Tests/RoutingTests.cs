using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests;

public class RoutingTests
{
    private static RoutingConfiguration MakeConfig(string alias, string nativeId)
    {
        var route = new ConfiguredRoute
        {
            RequestedModelAlias = alias,
            Enabled = true,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test-provider"),
                LogicalModelId.From(alias),
                ModelRouteId.From($"route:{alias}"),
                nativeId, true, ModelCapability.None)
        };
        return new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
    }

    [Fact]
    public async Task A_ExactRouteMatch_ReturnsConfiguredRoute()
    {
        var config = MakeConfig("client-model", "provider-model");
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest
        {
            RequestedModel = "client-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Route);
        Assert.Equal("client-model", result.Route!.LogicalModelId.Value);
    }

    [Fact]
    public async Task B_UnknownRequestedModel_ReturnsTypedFailure()
    {
        var config = MakeConfig("client-model", "provider-model");
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest
        {
            RequestedModel = "unknown-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureValue);
        Assert.Equal(RouteResolutionReason.UnknownRequestedModel, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task C_UnknownModel_DoesNotCallProvider()
    {
        var config = MakeConfig("client-model", "provider-model");
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest
        {
            RequestedModel = "unknown-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.UnknownRequestedModel, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task D_KnownModel_ExecutesProvider()
    {
        var config = MakeConfig("client-model", "provider-model");
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest
        {
            RequestedModel = "client-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Route);
    }

    [Fact]
    public async Task E_IdentitySeparation_PublicAliasVsProviderNativeId()
    {
        var config = MakeConfig("client-model", "provider-model");
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest
        {
            RequestedModel = "client-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("provider-model", result.Route!.ProviderNativeModelId);
    }

    [Fact]
    public async Task F_ChatExecutionService_ReceivesExplicitModelRoute_NoRouteResolver()
    {
        var route = new ConfiguredRoute
        {
            RequestedModelAlias = "client-model",
            Enabled = true,
            ModelRoute = ModelRoute.FromIds(
                ProviderId.From("test"),
                LogicalModelId.From("client-model"),
                ModelRouteId.From("r1"))
        };
        var prop = route.ModelRoute.GetType().GetProperty("ProviderNativeModelId");
        prop?.SetValue(route.ModelRoute, "provider-model");

        var mockProvider = new InlineFakeProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
        var request = new CanonicalChatRequest
        {
            RequestedModel = "client-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };
        var outcome = await service.ExecuteAsync(request, route.ModelRoute, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        Assert.Equal(1, mockProvider.CapturedCount);
    }

    private sealed class InlineFakeProvider : IChatCompletionProvider
    {
        private readonly CanonicalChatResponse _response;
        public int CapturedCount { get; private set; }
        public InlineFakeProvider(CanonicalChatResponse response) { _response = response; }
        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            CapturedCount++;
            return Task.FromResult(ProviderExecutionOutcome.Success(_response));
        }
    }

    // Slice 5: multiple candidates + hard eligibility

    [Fact]
    public async Task Slice5_A_MultipleCandidates_Discovered()
    {
        var route1 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var route2 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route1, route2 } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Slice5_B_DisabledRoute_Filtered()
    {
        var enabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", false, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled, enabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r1", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice5_E_NoEligibleRoute_TypedFailure()
    {
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", false, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task Slice5_H_NoEligibleRoute_DoesNotCallProvider()
    {
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", false, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task Slice5_I_SelectedEligibleRoute_CallsProviderExactlyOnce()
    {
        var eligible = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var ineligible = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", false, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { ineligible, eligible } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r1", result.Route!.ModelRouteId.Value);
    }

    // Slice 6: tool capability signal + hard eligibility

    [Fact]
    public async Task Slice6_A_NoTools_PlainRequest_NonToolsRouteEligible()
    {
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.None };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Slice6_B_ToolsPresent_NonToolsRouteFiltered()
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
    public async Task Slice6_C_ToolCapableRouteSelected()
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
    public async Task Slice6_D_NoToolCapableRoute_NoEligibleRoute()
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
    public async Task Slice6_E_NoEligibleToolRoute_DoesNotCallProvider()
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
    public async Task Slice6_F_SelectedToolCapableRoute_ExecutesOnce()
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
    public async Task Slice6_G_PlainRequest_DoesNotRequireTools()
    {
        var noTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { noTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.None };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Slice6_H_ToolChoiceNone_PlainRequest_Eligible()
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
    public async Task Slice6_I_ToolChoiceAuto_RequiresTools()
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
    public async Task Slice6_J_ExplicitRequiredToolChoice_RequiresTools()
    {
        var withTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.Tools) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { withTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Required };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    // Slice 7: declared quality tier routing

    [Fact]
    public async Task Slice7_A_PreferredBeatsAcceptable()
    {
        var preferred = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptable = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { acceptable, preferred } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-pref", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice7_B_AcceptableBeatsFallback()
    {
        var acceptable = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r-acc"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        var fallback = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Fallback, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r-fb"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Fallback) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { fallback, acceptable } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-acc", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice7_C_FallbackSelectedWhenOnlyFallback()
    {
        var fallback = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Fallback, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r-fb"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Fallback) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { fallback } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-fb", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice7_D_IneligiblePreferredDoesNotBeatEligibleAcceptable()
    {
        var preferredDisabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref"), "provider-model", false, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptableEnabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { preferredDisabled, acceptableEnabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-acc", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice7_E_ToolIncompatiblePreferredDoesNotBeatToolCapableAcceptable()
    {
        var preferredNoTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptableTools = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc"), "provider-model", true, ModelCapability.Tools, DeclaredQualityTier.Acceptable) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { preferredNoTools, acceptableTools } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }, ToolRequirement = ToolRequirement.Optional };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-acc", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice7_F_StableOrderInsidePreferredTier()
    {
        var pref1 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref1"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var pref2 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-pref2"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { pref1, pref2 } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-pref1", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice7_G_StableOrderInsideAcceptableTier()
    {
        var acc1 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-acc1"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        var acc2 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc2"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { acc1, acc2 } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-acc1", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice7_H_UnknownModel_Unchanged()
    {
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "unknown", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.UnknownRequestedModel, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task Slice7_I_NoEligibleRoute_Unchanged()
    {
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", false, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task Slice7_J_ProviderExecutesSelectedQualityRouteExactlyOnce()
    {
        var preferred = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r-pref"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var acceptable = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Acceptable, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r-acc"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Acceptable) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { acceptable, preferred } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-pref", result.Route!.ModelRouteId.Value);
    }

    // Slice 8: runtime route health state foundation

    [Fact]
    public async Task Slice8_A_UnknownHealth_Eligible()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new RouteOperationalEligibilityEvaluator(new HardRouteEligibilityEvaluator(), health), new RouteHealthRanker(health), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Slice8_B_Healthy_Eligible()
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
    public async Task Slice8_C_Degraded_Eligible()
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
    public async Task Slice8_D_Unavailable_Filtered()
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
    public async Task Slice8_E_AllUnavailable_NoEligibleRoute()
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
    public async Task Slice8_F_Unavailable_DoesNotCallProvider()
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
    public async Task Slice8_G_DegradedPreferredStillBeatsHealthyAcceptable()
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
    public async Task Slice8_H_StaticIneligibleStillWins()
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
    public async Task Slice8_I_ToolIncompatiblePreferredDoesNotBeatToolCapableAcceptable()
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
    public async Task Slice8_J_HealthProvider_DefaultUnknown()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var routeId = ModelRouteId.From("unknown-route");
        Assert.Equal(RouteHealthStatus.Unknown, health.GetHealth(routeId));
    }

    [Fact]
    public async Task Slice8_K_HealthUpdate_Observable()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        health.SetHealth(route.ModelRoute.ModelRouteId, RouteHealthStatus.Unavailable);
        Assert.Equal(RouteHealthStatus.Unavailable, health.GetHealth(route.ModelRoute.ModelRouteId));
    }

    [Fact]
    public async Task Slice8_L_AvailableRoute_ExecutesOnce()
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
    public async Task Slice5_G_DeterministicSelection_ConfigurationOrder()
    {
        var r1 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var r2 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { r1, r2 } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r1", result.Route!.ModelRouteId.Value);
    }

    // Slice 10: health-aware operational ordering within declared quality tier

    [Fact]
    public async Task Slice10_A_HealthyBeatsUnknown_WithinPreferred()
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
    public async Task Slice10_B_UnknownBeatsDegraded_WithinPreferred()
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
    public async Task Slice10_C_HealthyBeatsDegraded_WithinPreferred()
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
    public async Task Slice10_D_QualityTierOutranksHealth_PreferredDegradedVsAcceptableHealthy()
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
    public async Task Slice10_E_QualityTierOutranksUnknownVsHealthyDifference()
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
    public async Task Slice10_F_SameTierSameHealthy_PreservesConfigOrder()
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
    public async Task Slice10_G_SameTierSameUnknown_PreservesConfigOrder()
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
    public async Task Slice10_H_SameTierSameDegraded_PreservesConfigOrder()
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
    public async Task Slice10_I_UnavailableStillFilteredBeforeRanking()
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
    public async Task Slice10_J_AllUnavailable_RemainsNoEligibleRoute()
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
    public async Task Slice10_K_PassiveFailureChangesNextSameTierSelection()
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
    public async Task Slice10_L_SuccessRestoresPreferredSameTierRoute()
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
    public async Task Slice10_M_ProviderExecutesSelectedHealthyRouteExactlyOnce()
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
    public async Task Slice10_N_ToolsStaticEligibilityPrecedesHealth()
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
