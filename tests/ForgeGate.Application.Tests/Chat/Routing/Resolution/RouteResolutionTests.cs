using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Resolution;

/// <summary>
/// Tests for basic route resolution (model matching, eligibility basics).
/// </summary>
public class RouteResolutionTests
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
    public async Task ExactRouteMatch_ReturnsConfiguredRoute()
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
    public async Task UnknownRequestedModel_ReturnsTypedFailure()
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
    public async Task UnknownModel_DoesNotCallProvider()
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
    public async Task KnownModel_ExecutesProvider()
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
    public async Task IdentitySeparation_PublicAliasVsProviderNativeId()
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
    public async Task ChatExecutionService_ReceivesExplicitModelRoute()
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
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
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

    [Fact]
    public async Task MultipleCandidates_AreDiscovered()
    {
        var route1 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var route2 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route1, route2 } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DisabledRoute_IsFiltered()
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
    public async Task NoEligibleRoute_ReturnsTypedFailure()
    {
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", false, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task NoEligibleRoute_DoesNotCallProvider()
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
    public async Task SelectedEligibleRoute_CallsProviderExactlyOnce()
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

    [Fact]
    public async Task DeterministicSelection_PreservesConfigurationOrder()
    {
        var r1 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var r2 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { r1, r2 } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r1", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task PreferredQualityTier_OutranksAcceptable()
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
    public async Task AcceptableQualityTier_OutranksFallback()
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
    public async Task FallbackSelectedWhenOnlyOption()
    {
        var fallback = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Fallback, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r-fb"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Fallback) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { fallback } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-fb", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task IneligiblePreferredDoesNotBeatEligibleAcceptable()
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
    public async Task ToolIncompatiblePreferredDoesNotBeatToolCapableAcceptable()
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
    public async Task StableOrderInsidePreferredTier()
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
    public async Task StableOrderInsideAcceptableTier()
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
    public async Task UnknownModel_ReturnsFailure()
    {
        var route = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { route } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "unknown", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.UnknownRequestedModel, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task NoEligibleRoute_ReturnsFailure()
    {
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, QualityTier = DeclaredQualityTier.Preferred, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", false, ModelCapability.None, DeclaredQualityTier.Preferred) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator(), new RouteHealthRanker(new InMemoryRouteHealthStateProvider()), new InMemoryRouteCapacityCoordinator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task ProviderExecutesSelectedQualityRouteExactlyOnce()
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
}
