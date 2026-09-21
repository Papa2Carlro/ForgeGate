using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

public class Layer3EvaluatorTests
{
    [Fact]
    public void NoLayer3Evaluator_ReturnsNullLayer3Result()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var service = new AgentGuardService(normalizer, translator, evaluator);

        var action = new AgentAction
        {
            Source = "test",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Layer3Result);
    }

    [Fact]
    public void WithLayer3Evaluator_ReturnsNoRiskFoundForSafeAction()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var layer3Evaluator = new BasicLayer3Evaluator();
        var service = new AgentGuardService(normalizer, translator, evaluator, layer3Evaluator);

        var action = new AgentAction
        {
            Source = "test",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Layer3Result);
        Assert.False(result.Layer3Result.HasRiskFinding);
    }

    [Fact]
    public void Layer3EvaluatorDoesNotAffectPolicyDecision()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new FakeNormalizerReturningFileDelete();
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var layer3Evaluator = new BasicLayer3Evaluator();
        var service = new AgentGuardService(normalizer, translator, evaluator, layer3Evaluator);

        var action = new AgentAction
        {
            Source = "test",
            RawAction = "delete /workspace/old.txt"
        };

        var result = service.Evaluate(action);

        // Policy should still be RequireHumanApproval regardless of Layer 3
        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.RequireHumanApproval, result.Decision);
        Assert.NotNull(result.Layer3Result);
    }

    private class FakeNormalizerReturningFileDelete : IActionIntentNormalizer
    {
        public ActionIntentResult Normalize(AgentAction action) =>
            ActionIntentResult.Success(new ActionIntent
            {
                Capability = ActionIntentKind.FileDelete,
                Target = "/workspace/old.txt"
            });
    }

    [Fact]
    public void Layer3EvaluatorCalledAfterPolicyEvaluation()
    {
        var callOrder = new List<string>();
        var registry = new InMemoryCapabilityRegistry();
        var trackingNormalizer = new TrackingNormalizer(callOrder);
        var trackingTranslator = new TrackingTranslator(callOrder);
        var trackingEvaluator = new TrackingEvaluator(callOrder);
        var trackingLayer3 = new TrackingLayer3Evaluator(callOrder);
        var service = new AgentGuardService(trackingNormalizer, trackingTranslator, trackingEvaluator, trackingLayer3);

        var action = new AgentAction
        {
            Source = "test",
            RawAction = "cat /workspace/foo.txt"
        };

        service.Evaluate(action);

        Assert.Contains("Normalize", callOrder);
        Assert.Contains("Translate", callOrder);
        Assert.Contains("Evaluate", callOrder);
        Assert.Contains("Layer3", callOrder);

        var evaluateIndex = callOrder.IndexOf("Evaluate");
        var layer3Index = callOrder.IndexOf("Layer3");
        Assert.True(evaluateIndex < layer3Index, "Layer 3 should be called after policy evaluation");
    }

    private class TrackingNormalizer : IActionIntentNormalizer
    {
        private readonly List<string> _callOrder;
        public TrackingNormalizer(List<string> callOrder) => _callOrder = callOrder;
        public ActionIntentResult Normalize(AgentAction action)
        {
            _callOrder.Add("Normalize");
            return ActionIntentResult.Success(new ActionIntent
            {
                Capability = ActionIntentKind.FileRead,
                Target = "/workspace/foo.txt"
            });
        }
    }

    private class TrackingTranslator : ICapabilityTranslator
    {
        private readonly List<string> _callOrder;
        public TrackingTranslator(List<string> callOrder) => _callOrder = callOrder;
        public CapabilityTranslationResult Translate(ActionIntent intent)
        {
            _callOrder.Add("Translate");
            return CapabilityTranslationResult.Success(intent.Capability, intent.Target, intent.Metadata);
        }
    }

    private class TrackingEvaluator : IPolicyEvaluator
    {
        private readonly List<string> _callOrder;
        public TrackingEvaluator(List<string> callOrder) => _callOrder = callOrder;
        public PolicyEvaluationResult Evaluate(CapabilityTranslationResult translation)
        {
            _callOrder.Add("Evaluate");
            return PolicyEvaluationResult.Evaluate(PolicyDecision.Allow, "allowed");
        }
    }

    private class TrackingLayer3Evaluator : ILayer3Evaluator
    {
        private readonly List<string> _callOrder;
        public TrackingLayer3Evaluator(List<string> callOrder) => _callOrder = callOrder;
        public Layer3EvaluationResult Evaluate(ActionIntentKind capability, string? contextualEvidence = null)
        {
            _callOrder.Add("Layer3");
            return Layer3EvaluationResult.NoRiskFound;
        }
    }
}
