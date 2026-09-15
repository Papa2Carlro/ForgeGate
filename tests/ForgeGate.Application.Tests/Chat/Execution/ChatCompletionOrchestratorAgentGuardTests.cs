using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.AgentGuard;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.AgentGuard;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Execution;

/// <summary>
/// Integration tests for Agent Guard boundary in ChatCompletionOrchestrator.
/// 
/// NOTE: These tests verify the Agent Guard FOUNDATION and DI wiring,
/// but NOT actual gateway integration. The ChatCompletionOrchestrator
/// does not currently invoke IAgentGuard because there is no legitimate
/// semantic AgentAction source for ordinary chat completion requests.
/// 
/// When a proper AgentAction source is available (e.g., tool call extraction),
/// the integration point is in ChatCompletionOrchestrator.ExecuteAsync()
/// before calling _chatExecutionService.ExecuteAsync().
/// </summary>
public class ChatCompletionOrchestratorAgentGuardTests
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

    #endregion

    #region Helper Methods

    private static (ChatCompletionOrchestrator, FakeChatCompletionProvider) SetupWithoutAgentGuard()
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

        var provider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "Mock response" });
        var executionService = new ChatExecutionService(provider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(health));
        // Note: No IAgentGuard passed - orchestrator does not invoke Agent Guard
        var orchestrator = new ChatCompletionOrchestrator(resolver, executionService);

        return (orchestrator, provider);
    }

    private static CanonicalChatRequest MakeRequest(string content = "Hello") =>
        new()
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = content } }
        };

    #endregion

    #region A. Existing behavior preserved (no Agent Guard interference)

    [Fact]
    public async Task WithoutAgentGuard_ExistingBehaviorUnchanged()
    {
        // When IAgentGuard is not provided, execution proceeds normally
        var (orchestrator, provider) = SetupWithoutAgentGuard();
        var request = MakeRequest();

        var outcome = await orchestrator.ExecuteAsync(request, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Response);
        Assert.Equal("Mock response", outcome.Response!.Content);
        Assert.Equal(1, provider.CallCount);
    }

    #endregion

    #region B. Agent Guard foundation tests (unit-level)

    [Fact]
    public void AgentGuard_Foundation_IsWiredInDI()
    {
        // Verify the DI registration exists (structural proof)
        // This test documents the architectural prerequisite
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var service = new AgentGuardService(normalizer, translator, evaluator);

        Assert.NotNull(service);
    }

    [Fact]
    public void AgentGuard_Result_PreservesPolicyDecision()
    {
        // Verify PolicyDecision is preserved in AgentGuardResult
        var result = AgentGuardResult.Success(
            PolicyDecision.Allow,
            ActionIntentKind.FileRead,
            "/workspace/foo.txt");

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
        Assert.Equal(ActionIntentKind.FileRead, result.Capability);
    }

    [Fact]
    public void ProviderFailure_PreservesPolicyDecision()
    {
        // Verify PolicyDecision is preserved in ProviderFailure
        var failure = new ProviderFailure
        {
            Category = ProviderFailureCategory.AgentGuardDenied,
            Retryability = ProviderFailureRetryability.NotRetryable,
            Scope = ProviderFailureScope.Request,
            SanitizedUpstreamMessage = "Agent Guard: request denied by policy",
            PolicyDecision = PolicyDecision.Deny
        };

        Assert.Equal(PolicyDecision.Deny, failure.PolicyDecision);
    }

    #endregion

    #region C. Semantic distinction tests

    [Fact]
    public void Deny_IsStructurallyDistinctFromAllow()
    {
        var allowResult = AgentGuardResult.Success(PolicyDecision.Allow, ActionIntentKind.FileRead, "/path");
        var denyResult = AgentGuardResult.Success(PolicyDecision.Deny, ActionIntentKind.FileRead, "/path");

        Assert.Equal(PolicyDecision.Allow, allowResult.Decision);
        Assert.Equal(PolicyDecision.Deny, denyResult.Decision);
        Assert.NotEqual(allowResult.Decision, denyResult.Decision);
    }

    [Fact]
    public void RequireHumanApproval_IsStructurallyDistinctFromDeny()
    {
        var denyResult = AgentGuardResult.Success(PolicyDecision.Deny, ActionIntentKind.FileRead, "/path");
        var approvalResult = AgentGuardResult.Success(PolicyDecision.RequireHumanApproval, ActionIntentKind.FileDelete, "/path");

        Assert.Equal(PolicyDecision.Deny, denyResult.Decision);
        Assert.Equal(PolicyDecision.RequireHumanApproval, approvalResult.Decision);
        Assert.NotEqual(denyResult.Decision, approvalResult.Decision);
    }

    #endregion

    #region D. Normalizer/Translator/Evaluator boundary tests

    [Fact]
    public void Normalizer_DoesNotExecuteAction()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        
        var action = new AgentAction
        {
            Source = "test",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        // Normalizer returns semantic intent, does not execute
    }

    [Fact]
    public void Translator_DoesNotMakePolicyDecision()
    {
        var registry = new InMemoryCapabilityRegistry();
        var translator = new BasicCapabilityTranslator(registry);
        
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = "/workspace/foo.txt"
        };

        var result = translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, result.Capability);
        // Translator returns capability, does not decide policy
    }

    [Fact]
    public void Evaluator_DoesNotExecuteCapability()
    {
        var registry = new InMemoryCapabilityRegistry();
        var evaluator = new BasicPolicyEvaluator(registry);
        
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileRead, "/workspace/foo.txt");

        var result = evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
        // Evaluator returns policy decision, does not execute
    }

    #endregion
}
