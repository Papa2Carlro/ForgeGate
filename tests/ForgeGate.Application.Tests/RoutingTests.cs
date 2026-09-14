using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing;
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
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
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
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
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
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
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
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
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
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
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
        var service = new ChatExecutionService(mockProvider);
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
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Slice5_B_DisabledRoute_Filtered()
    {
        var enabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", false, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled, enabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r1", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice5_E_NoEligibleRoute_TypedFailure()
    {
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", false, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(RouteResolutionReason.NoEligibleRoute, result.FailureValue!.Reason);
    }

    [Fact]
    public async Task Slice5_H_NoEligibleRoute_DoesNotCallProvider()
    {
        var disabled = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = false, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", false, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { disabled } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
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
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
        var request = new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } };
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r1", result.Route!.ModelRouteId.Value);
    }

    [Fact]
    public async Task Slice5_G_DeterministicSelection_ConfigurationOrder()
    {
        var r1 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p1"), LogicalModelId.From("m"), ModelRouteId.From("r1"), "provider-model", true, ModelCapability.None) };
        var r2 = new ConfiguredRoute { RequestedModelAlias = "client-model", Enabled = true, ModelRoute = ModelRoute.FromIdsWithOptions(ProviderId.From("p2"), LogicalModelId.From("m"), ModelRouteId.From("r2"), "provider-model", true, ModelCapability.None) };
        var config = new RoutingConfiguration { Routes = new List<ConfiguredRoute> { r1, r2 } };
        var resolver = new ConfiguredRouteResolver(Options.Create(config), new HardRouteEligibilityEvaluator());
        var result = await resolver.ResolveAsync(new CanonicalChatRequest { RequestedModel = "client-model", Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } } }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r1", result.Route!.ModelRouteId.Value);
    }
}
