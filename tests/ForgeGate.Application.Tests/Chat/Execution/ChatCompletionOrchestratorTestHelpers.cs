using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Execution;

/// <summary>
/// Shared test helpers and test doubles for ChatCompletionOrchestrator tests.
/// </summary>
internal static class OrchestratorTestHelpers
{
    public static RoutingConfiguration MakeConfig(params (string alias, string nativeId, DeclaredQualityTier tier, int concurrencyLimit)[] routeSpecs)
    {
        var routes = new List<ConfiguredRoute>();
        foreach (var (alias, nativeId, tier, concurrencyLimit) in routeSpecs)
        {
            var route = ModelRoute.FromIdsWithOptions(
                ProviderId.From(alias),
                LogicalModelId.From("gpt-4"),
                ModelRouteId.From($"{alias}:{nativeId}"),
                nativeId,
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: tier,
                maxConcurrentExecutions: concurrencyLimit);
            routes.Add(new ConfiguredRoute
            {
                RequestedModelAlias = "gpt-4",
                Enabled = true,
                QualityTier = tier,
                ModelRoute = route
            });
        }
        return new RoutingConfiguration { Routes = routes };
    }

    public static (ChatCompletionOrchestrator, IRouteResolver, InMemoryRouteHealthStateProvider, FakeChatCompletionProvider)
        SetupWithSingleRoute(DeclaredQualityTier tier = DeclaredQualityTier.Preferred, int concurrencyLimit = 100)
    {
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeConfig(("gpt-4", "gpt-4-turbo", tier, concurrencyLimit));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var provider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "response1" });
        var executionService = new ChatExecutionService(provider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        return (orchestrator, resolver, health, provider);
    }

    public static (ChatCompletionOrchestrator, IRouteResolver, InMemoryRouteHealthStateProvider, FakeChatCompletionProvider)
        SetupWithTwoRoutes(DeclaredQualityTier tier = DeclaredQualityTier.Preferred, int concurrencyLimit = 100)
    {
        var health = new InMemoryRouteHealthStateProvider();
        var config = MakeConfig(
            ("gpt-4", "gpt-4-turbo", tier, concurrencyLimit),
            ("gpt-4-2", "gpt-4-turbo-2", tier, concurrencyLimit));
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var provider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "fallback-response" });
        var executionService = new ChatExecutionService(provider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        return (orchestrator, resolver, health, provider);
    }

    public static CanonicalChatRequest MakeRequest(string model = "gpt-4") =>
        new()
        {
            RequestedModel = model,
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };
}

/// <summary>
/// Test double that returns different responses based on call order and route.
/// </summary>
public sealed class FakeFailoverProvider : IChatCompletionProvider
{
    private readonly Queue<(ModelRouteId? expectedRouteId, object response)> _responses;

    public FakeFailoverProvider(params (ModelRouteId? expectedRouteId, object response)[] responses)
    {
        _responses = new Queue<(ModelRouteId?, object)>(responses);
    }

    public FakeFailoverProvider(params object[] responses)
    {
        _responses = new Queue<(ModelRouteId?, object)>();
        foreach (var response in responses)
            _responses.Enqueue((null, response));
    }

    public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
    {
        var (expectedRouteId, response) = _responses.Dequeue();
        // If we have an expected route ID and it doesn't match, swap to next response
        if (expectedRouteId.HasValue && expectedRouteId.Value != route.ModelRouteId && _responses.Count > 0)
        {
            _responses.Enqueue((expectedRouteId, response));
            (expectedRouteId, response) = _responses.Dequeue();
        }
        return response is ProviderFailure failure
            ? Task.FromResult(ProviderExecutionOutcome.Failure(failure))
            : Task.FromResult(ProviderExecutionOutcome.Success((CanonicalChatResponse)response));
    }
}

/// <summary>
/// Test double that tracks call count and returns a specific failure.
/// </summary>
public sealed class FakeCountingProvider : IChatCompletionProvider
{
    private readonly ProviderFailure _failure;
    private readonly Action _onCall;

    public FakeCountingProvider(ProviderFailure failure, Action onCall)
    {
        _failure = failure;
        _onCall = onCall;
    }

    public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
    {
        _onCall();
        return Task.FromResult(ProviderExecutionOutcome.Failure(_failure));
    }
}

/// <summary>
/// Test double that returns success with null response.
/// </summary>
public sealed class FakeNullResponseProvider : IChatCompletionProvider
{
    public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(ProviderExecutionOutcome.Success(null!));
    }
}
