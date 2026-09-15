using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

public class AgentGuardServiceTests
{
    private readonly InMemoryCapabilityRegistry _registry;
    private readonly BasicActionIntentNormalizer _normalizer;
    private readonly BasicCapabilityTranslator _translator;
    private readonly BasicPolicyEvaluator _evaluator;
    private readonly AgentGuardService _service;

    public AgentGuardServiceTests()
    {
        _registry = new InMemoryCapabilityRegistry();
        _normalizer = new BasicActionIntentNormalizer(_registry);
        _translator = new BasicCapabilityTranslator(_registry);
        _evaluator = new BasicPolicyEvaluator(_registry);
        _service = new AgentGuardService(_normalizer, _translator, _evaluator);
    }

    // =====================================================================
    // Successful pipeline cases
    // =====================================================================

    [Fact]
    public void CatCommand_ProducesAllow()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = _service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
        Assert.Equal(ActionIntentKind.FileRead, result.Capability);
        Assert.Equal("/workspace/foo.txt", result.Target);
    }

    [Fact]
    public void LsCommand_ProducesAllow()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "ls /workspace/src"
        };

        var result = _service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
        Assert.Equal(ActionIntentKind.DirectoryList, result.Capability);
        Assert.Equal("/workspace/src", result.Target);
    }

    [Fact]
    public void GrepCommand_ProducesAllow()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "grep TODO /workspace/src"
        };

        var result = _service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
        Assert.Equal(ActionIntentKind.FileSearch, result.Capability);
        Assert.Equal("/workspace/src", result.Target);
        Assert.Equal("TODO", result.Metadata);
    }

    // =====================================================================
    // Destructive capability through full pipeline
    // =====================================================================

    [Fact]
    public void DestructiveCapability_ProducesRequireHumanApproval()
    {
        // Use a fake normalizer to simulate FileDelete (production normalizer
        // doesn't recognize "delete" command yet)
        var fakeNormalizer = new FakeNormalizerReturningFileDelete();
        var service = new AgentGuardService(fakeNormalizer, _translator, _evaluator);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "delete /workspace/old.txt"
        };

        var result = service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.RequireHumanApproval, result.Decision);
        Assert.Equal(ActionIntentKind.FileDelete, result.Capability);
        Assert.Equal("/workspace/old.txt", result.Target);
    }

    // =====================================================================
    // Deny policy decision preserved through pipeline
    // =====================================================================

    [Fact]
    public void DenyPolicyDecision_IsPreserved()
    {
        // Use a fake evaluator that returns Deny
        var fakeEvaluator = new FakeEvaluatorReturningDeny();
        var service = new AgentGuardService(_normalizer, _translator, fakeEvaluator);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Deny, result.Decision);
        Assert.Equal(ActionIntentKind.FileRead, result.Capability);
    }

    // =====================================================================
    // Failure short-circuiting
    // =====================================================================

    [Fact]
    public void UnknownAction_FailsAtNormalization()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "teleport_to_mars"
        };

        var result = _service.Evaluate(action);

        Assert.False(result.IsSuccess);
        Assert.Equal("Normalization", result.FailureStage);
        Assert.Equal("teleport_to_mars", result.FailureReason);
    }

    [Fact]
    public void EmptyAction_FailsAtNormalization()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = string.Empty
        };

        var result = _service.Evaluate(action);

        Assert.False(result.IsSuccess);
        Assert.Equal("Normalization", result.FailureStage);
    }

    [Fact]
    public void DangerousSyntax_FailsAtNormalization()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "rm -rf /workspace/*"
        };

        var result = _service.Evaluate(action);

        Assert.False(result.IsSuccess);
        Assert.Equal("Normalization", result.FailureStage);
    }

    [Fact]
    public void TranslationFailure_FailsAtTranslation()
    {
        var callOrder = new List<string>();
        var trackingNormalizer = new TrackingNormalizer(callOrder);
        var trackingTranslator = new TrackingTranslatorReturningFailure(callOrder);
        var trackingEvaluator = new TrackingEvaluator(callOrder);

        var service = new AgentGuardService(trackingNormalizer, trackingTranslator, trackingEvaluator);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.False(result.IsSuccess);
        Assert.Equal("Translation", result.FailureStage);
        Assert.Contains("Normalize", callOrder);
        Assert.Contains("Translate", callOrder);
        Assert.DoesNotContain("Evaluate", callOrder);
    }

    [Fact]
    public void PolicyEvaluationFailure_FailsAtPolicy()
    {
        var callOrder = new List<string>();
        var trackingNormalizer = new TrackingNormalizer(callOrder);
        var trackingTranslator = new TrackingTranslator(callOrder);
        var trackingEvaluator = new TrackingEvaluatorReturningFailure(callOrder);

        var service = new AgentGuardService(trackingNormalizer, trackingTranslator, trackingEvaluator);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.False(result.IsSuccess);
        Assert.Equal("PolicyEvaluation", result.FailureStage);
        Assert.Contains("Normalize", callOrder);
        Assert.Contains("Translate", callOrder);
        Assert.Contains("Evaluate", callOrder);
    }

    // =====================================================================
    // Semantic data preservation
    // =====================================================================

    [Fact]
    public void SemanticCapability_IsPreserved()
    {
        var action = new AgentAction
        {
            Source = "gpt-4",
            RawAction = "cat /workspace/important_document.pdf"
        };

        var result = _service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, result.Capability);
    }

    [Fact]
    public void TargetPath_IsPreserved()
    {
        var action = new AgentAction
        {
            Source = "gpt-4",
            RawAction = "cat /workspace/important_document.pdf"
        };

        var result = _service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal("/workspace/important_document.pdf", result.Target);
    }

    [Fact]
    public void SearchMetadata_IsPreserved()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "grep ERROR /workspace/logs"
        };

        var result = _service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal("ERROR", result.Metadata);
    }

    // =====================================================================
    // Separation of concerns
    // =====================================================================

    [Fact]
    public void NullAction_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Evaluate(null!));
    }

    // =====================================================================
    // Call order verification
    // =====================================================================

    [Fact]
    public void PipelineExercisesAllStages()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = _service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void NormalizerCalledBeforeTranslator()
    {
        var callOrder = new List<string>();

        var trackingNormalizer = new TrackingNormalizer(callOrder);
        var trackingTranslator = new TrackingTranslator(callOrder);
        var trackingEvaluator = new TrackingEvaluator(callOrder);

        var service = new AgentGuardService(trackingNormalizer, trackingTranslator, trackingEvaluator);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        service.Evaluate(action);

        Assert.Contains("Normalize", callOrder);
        Assert.Contains("Translate", callOrder);
        Assert.Contains("Evaluate", callOrder);

        var normalizeIndex = callOrder.IndexOf("Normalize");
        var translateIndex = callOrder.IndexOf("Translate");
        var evaluateIndex = callOrder.IndexOf("Evaluate");

        Assert.True(normalizeIndex < translateIndex);
        Assert.True(translateIndex < evaluateIndex);
    }

    [Fact]
    public void FailedNormalizationPreventsTranslatorAndEvaluatorCalls()
    {
        var callOrder = new List<string>();

        var trackingNormalizer = new TrackingNormalizerReturningFailure(callOrder);
        var trackingTranslator = new TrackingTranslator(callOrder);
        var trackingEvaluator = new TrackingEvaluator(callOrder);

        var service = new AgentGuardService(trackingNormalizer, trackingTranslator, trackingEvaluator);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "unknown_command"
        };

        service.Evaluate(action);

        Assert.Contains("Normalize", callOrder);
        Assert.DoesNotContain("Translate", callOrder);
        Assert.DoesNotContain("Evaluate", callOrder);
    }

    // =====================================================================
    // Helper classes
    // =====================================================================

    private class FakeNormalizerReturningFileDelete : IActionIntentNormalizer
    {
        public ActionIntentResult Normalize(AgentAction action) =>
            ActionIntentResult.Success(new ActionIntent
            {
                Capability = ActionIntentKind.FileDelete,
                Target = "/workspace/old.txt"
            });
    }

    private class FakeEvaluatorReturningDeny : IPolicyEvaluator
    {
        public PolicyEvaluationResult Evaluate(CapabilityTranslationResult translation) =>
            PolicyEvaluationResult.Evaluate(PolicyDecision.Deny, "Denied by policy");
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
            return PolicyEvaluationResult.Evaluate(PolicyDecision.Allow, "Allowed");
        }
    }

    private class TrackingNormalizerReturningFailure : IActionIntentNormalizer
    {
        private readonly List<string> _callOrder;

        public TrackingNormalizerReturningFailure(List<string> callOrder) => _callOrder = callOrder;

        public ActionIntentResult Normalize(AgentAction action)
        {
            _callOrder.Add("Normalize");
            return ActionIntentResult.Unknown(action.RawAction);
        }
    }

    private class TrackingTranslatorReturningFailure : ICapabilityTranslator
    {
        private readonly List<string> _callOrder;

        public TrackingTranslatorReturningFailure(List<string> callOrder) => _callOrder = callOrder;

        public CapabilityTranslationResult Translate(ActionIntent intent)
        {
            _callOrder.Add("Translate");
            return CapabilityTranslationResult.Failure(new TranslationFailure("Failed"));
        }
    }

    private class TrackingEvaluatorReturningFailure : IPolicyEvaluator
    {
        private readonly List<string> _callOrder;

        public TrackingEvaluatorReturningFailure(List<string> callOrder) => _callOrder = callOrder;

        public PolicyEvaluationResult Evaluate(CapabilityTranslationResult translation)
        {
            _callOrder.Add("Evaluate");
            return PolicyEvaluationResult.Failure(new PolicyEvaluationFailure("Policy failed"));
        }
    }
}
