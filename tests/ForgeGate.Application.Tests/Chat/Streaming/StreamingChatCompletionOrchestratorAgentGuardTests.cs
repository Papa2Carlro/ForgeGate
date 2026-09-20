using ForgeGate.Application.AgentGuard;
using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.AgentGuard;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Streaming;

/// <summary>
/// Integration tests for Agent Guard boundary in StreamingChatCompletionOrchestrator.
/// </summary>
public class StreamingChatCompletionOrchestratorAgentGuardTests
{
    #region Test Doubles

    private sealed class FakeAgentGuardAllow : IAgentGuard
    {
        public AgentGuardResult Evaluate(AgentAction action) =>
            AgentGuardResult.Success(PolicyDecision.Allow, ActionIntentKind.FileRead, "/workspace/foo.txt");
    }

    private sealed class FakeAgentGuardDeny : IAgentGuard
    {
        public AgentGuardResult Evaluate(AgentAction action) =>
            AgentGuardResult.Success(PolicyDecision.Deny, ActionIntentKind.FileRead, "/workspace/foo.txt");
    }

    private sealed class FakeAgentGuardRequireApproval : IAgentGuard
    {
        public AgentGuardResult Evaluate(AgentAction action) =>
            AgentGuardResult.Success(PolicyDecision.RequireHumanApproval, ActionIntentKind.FileDelete, "/workspace/old.txt");
    }

    private sealed class FakeStreamingProviderWithToolCalls : IStreamingChatCompletionProvider
    {
        private readonly IReadOnlyList<ToolCallInvocation> _toolCalls;

        public FakeStreamingProviderWithToolCalls(IReadOnlyList<ToolCallInvocation> toolCalls)
        {
            _toolCalls = toolCalls;
        }

        public Task<StreamingExecutionOutcome> ExecuteStreamingAsync(
            ModelRoute route,
            StreamingChatRequest request,
            StreamingBuffer buffer,
            CancellationToken cancellationToken)
        {
            buffer.TryAddChunk("data: {}\n");
            buffer.Commit();
            return Task.FromResult(StreamingExecutionOutcome.Success(
                new[] { "data: {}\n" },
                true,
                route,
                _toolCalls));
        }
    }

    #endregion

    #region Helper Methods

    private static (StreamingChatCompletionOrchestrator, FakeStreamingProviderWithToolCalls) SetupWithAgentGuard(IAgentGuard agentGuard)
    {
        var health = new InMemoryRouteHealthStateProvider();
        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("openai"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("openai:gpt-4"),
                        "gpt-4-turbo",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Preferred,
                        maxConcurrentExecutions: 100)
                }
            }
        };

        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var toolCalls = new[] { new ToolCallInvocation { Id = "1", Index = 0, Name = "read_file", Arguments = "{\"path\": \"/workspace/foo.txt\"}" } };
        var provider = new FakeStreamingProviderWithToolCalls(toolCalls);
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService, agentGuard);

        return (orchestrator, provider);
    }

    private static (StreamingChatCompletionOrchestrator, FakeStreamingProviderWithToolCalls) SetupWithoutAgentGuard()
    {
        var health = new InMemoryRouteHealthStateProvider();
        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("openai"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("openai:gpt-4"),
                        "gpt-4-turbo",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Preferred,
                        maxConcurrentExecutions: 100)
                }
            }
        };

        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var toolCalls = new[] { new ToolCallInvocation { Id = "1", Index = 0, Name = "read_file", Arguments = "{\"path\": \"/workspace/foo.txt\"}" } };
        var provider = new FakeStreamingProviderWithToolCalls(toolCalls);
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService);

        return (orchestrator, provider);
    }

    private static StreamingChatRequest MakeStreamingRequest() =>
        new()
        {
            BaseRequest = new CanonicalChatRequest
            {
                RequestedModel = "gpt-4",
                Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
            }
        };

    #endregion

    #region A. Tool call interception tests

    [Fact]
    public async Task StreamingToolCall_Allowed_ProceedsNormally()
    {
        // When IAgentGuard is provided and allows the tool call, execution proceeds
        var (orchestrator, _) = SetupWithAgentGuard(new FakeAgentGuardAllow());
        var request = MakeStreamingRequest();
        var buffer = new StreamingBuffer();

        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.IsCommitted);
        Assert.NotEmpty(outcome.BufferedChunks);
    }

    [Fact]
    public async Task StreamingToolCall_Denied_ReturnsAgentGuardDenied()
    {
        var (orchestrator, _) = SetupWithAgentGuard(new FakeAgentGuardDeny());
        var request = MakeStreamingRequest();
        var buffer = new StreamingBuffer();

        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureCategory.AgentGuardDenied, outcome.FailureValue!.Category);
        Assert.Equal(PolicyDecision.Deny, outcome.FailureValue.PolicyDecision);
        Assert.Contains("denied by policy", outcome.FailureValue.SanitizedUpstreamMessage);
    }

    [Fact]
    public async Task StreamingToolCall_RequiresApproval_ReturnsPolicyDeniedWithApproval()
    {
        var (orchestrator, _) = SetupWithAgentGuard(new FakeAgentGuardRequireApproval());
        var request = MakeStreamingRequest();
        var buffer = new StreamingBuffer();

        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Equal(ProviderFailureCategory.AgentGuardDenied, outcome.FailureValue!.Category);
        Assert.Equal(PolicyDecision.RequireHumanApproval, outcome.FailureValue.PolicyDecision);
        Assert.Contains("requires human approval", outcome.FailureValue.SanitizedUpstreamMessage);
    }

    [Fact]
    public async Task StreamingNoToolCalls_PassesThroughWithoutGuard()
    {
        // When no tool calls are present, Agent Guard is not invoked
        var health = new InMemoryRouteHealthStateProvider();
        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new()
                {
                    RequestedModelAlias = "gpt-4",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = ModelRoute.FromIdsWithOptions(
                        ProviderId.From("openai"),
                        LogicalModelId.From("gpt-4"),
                        ModelRouteId.From("openai:gpt-4"),
                        "gpt-4-turbo",
                        enabled: true,
                        capabilities: ModelCapability.None,
                        qualityTier: DeclaredQualityTier.Preferred,
                        maxConcurrentExecutions: 100)
                }
            }
        };

        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(health),
            new InMemoryRouteCapacityCoordinator());

        var provider = new FakeStreamingProvider(new[] { "data: {}\n" });
        var executionService = new ChatExecutionService(new FakeChatCompletionProvider(), provider, new RouteHealthFeedback(health));
        var orchestrator = new StreamingChatCompletionOrchestrator(resolver, executionService, new FakeAgentGuardDeny());

        var request = MakeStreamingRequest();
        var buffer = new StreamingBuffer();
        var outcome = await orchestrator.ExecuteStreamingAsync(request, buffer, CancellationToken.None);

        // Should succeed because no tool calls means guard is skipped
        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.IsCommitted);
    }

    #endregion
}
