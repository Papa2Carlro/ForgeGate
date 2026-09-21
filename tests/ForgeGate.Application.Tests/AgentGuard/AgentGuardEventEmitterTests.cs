using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

/// <summary>
/// Tests for structured event emission in AgentGuardService.
/// </summary>
public class AgentGuardEventEmitterTests
{
    [Fact]
    public void NoEmitter_DefaultsToNullEmitter_SuccessPath()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var service = new AgentGuardService(normalizer, translator, evaluator);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void NullEmitter_DisablesEventEmission()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var service = new AgentGuardService(normalizer, translator, evaluator, emitter: NullAgentGuardEventEmitter.Instance);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void SuccessPath_EmitsPolicyEvaluatedEvent()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var trackingEmitter = new TrackingEventEmitter();
        var service = new AgentGuardService(normalizer, translator, evaluator, emitter: trackingEmitter);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Single(trackingEmitter.Events);
        var @event = trackingEmitter.Events[0];
        Assert.Equal(AgentGuardOutcomeType.PolicyEvaluated, @event.OutcomeType);
        Assert.Equal(PolicyDecision.Allow, @event.Decision);
        Assert.Equal(ActionIntentKind.FileRead, @event.Capability);
        Assert.Equal("/workspace/foo.txt", @event.Target);
        Assert.Null(@event.FailureStage);
        Assert.Null(@event.FailureReason);
    }

    [Fact]
    public void NormalizationFailure_EmitsNormalizationFailedEvent()
    {
        var registry = new InMemoryCapabilityRegistry();
        var trackingEmitter = new TrackingEventEmitter();
        var trackingNormalizer = new TrackingNormalizerReturningFailure();
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var service = new AgentGuardService(trackingNormalizer, translator, evaluator, emitter: trackingEmitter);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "teleport_to_mars"
        };

        var result = service.Evaluate(action);

        Assert.False(result.IsSuccess);
        Assert.Single(trackingEmitter.Events);
        var @event = trackingEmitter.Events[0];
        Assert.Equal(AgentGuardOutcomeType.NormalizationFailed, @event.OutcomeType);
        Assert.Null(@event.Decision);
        Assert.Equal("Normalization", @event.FailureStage);
        Assert.NotNull(@event.FailureReason);
    }

    [Fact]
    public void TranslationFailure_EmitsTranslationFailedEvent()
    {
        var registry = new InMemoryCapabilityRegistry();
        var trackingEmitter = new TrackingEventEmitter();
        var trackingNormalizer = new TrackingNormalizer();
        var trackingTranslator = new TrackingTranslatorReturningFailure();
        var evaluator = new BasicPolicyEvaluator(registry);
        var service = new AgentGuardService(trackingNormalizer, trackingTranslator, evaluator, emitter: trackingEmitter);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "unknown_command"
        };

        var result = service.Evaluate(action);

        Assert.False(result.IsSuccess);
        Assert.Single(trackingEmitter.Events);
        var @event = trackingEmitter.Events[0];
        Assert.Equal(AgentGuardOutcomeType.TranslationFailed, @event.OutcomeType);
        Assert.Null(@event.Decision);
        Assert.Equal("Translation", @event.FailureStage);
        Assert.NotNull(@event.FailureReason);
    }

    [Fact]
    public void PolicyEvaluationFailure_EmitsPolicyEvaluationFailedEvent()
    {
        var registry = new InMemoryCapabilityRegistry();
        var trackingEmitter = new TrackingEventEmitter();
        var trackingNormalizer = new TrackingNormalizer();
        var trackingTranslator = new TrackingTranslator();
        var trackingEvaluator = new TrackingEvaluatorReturningFailure();
        var service = new AgentGuardService(trackingNormalizer, trackingTranslator, trackingEvaluator, emitter: trackingEmitter);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.False(result.IsSuccess);
        Assert.Single(trackingEmitter.Events);
        var @event = trackingEmitter.Events[0];
        Assert.Equal(AgentGuardOutcomeType.PolicyEvaluationFailed, @event.OutcomeType);
        Assert.Null(@event.Decision);
        Assert.Equal("PolicyEvaluation", @event.FailureStage);
        Assert.NotNull(@event.FailureReason);
    }

    [Fact]
    public void SuccessWithLayer3_EmitsEventWithLayer3Result()
    {
        var registry = new InMemoryCapabilityRegistry();
        var trackingEmitter = new TrackingEventEmitter();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var layer3Evaluator = new BasicLayer3Evaluator();
        var service = new AgentGuardService(normalizer, translator, evaluator, layer3Evaluator, trackingEmitter);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = service.Evaluate(action);

        Assert.True(result.IsSuccess);
        Assert.Single(trackingEmitter.Events);
        var @event = trackingEmitter.Events[0];
        Assert.Equal(AgentGuardOutcomeType.PolicyEvaluated, @event.OutcomeType);
        Assert.NotNull(@event.Layer3Result);
        Assert.False(@event.Layer3Result.HasRiskFinding);
    }

    [Fact]
    public void EventContainsCorrelationId()
    {
        var registry = new InMemoryCapabilityRegistry();
        var trackingEmitter = new TrackingEventEmitter();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var service = new AgentGuardService(normalizer, translator, evaluator, emitter: trackingEmitter);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        service.Evaluate(action);

        Assert.Single(trackingEmitter.Events);
        var @event = trackingEmitter.Events[0];
        Assert.NotEqual(Guid.Empty, @event.Observation.CorrelationId);
    }

    [Fact]
    public void MultipleCalls_EmitMultipleEvents()
    {
        var registry = new InMemoryCapabilityRegistry();
        var trackingEmitter = new TrackingEventEmitter();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);
        var service = new AgentGuardService(normalizer, translator, evaluator, emitter: trackingEmitter);

        var action1 = new AgentAction { Source = "test", RawAction = "cat /workspace/foo.txt" };
        var action2 = new AgentAction { Source = "test", RawAction = "ls /workspace/src" };

        service.Evaluate(action1);
        service.Evaluate(action2);

        Assert.Equal(2, trackingEmitter.Events.Count);
    }

    #region Test Doubles

    private sealed class TrackingEventEmitter : IAgentGuardEventEmitter
    {
        public List<AgentGuardEvent> Events { get; } = new();

        public void Emit(AgentGuardEvent @event)
        {
            Events.Add(@event);
        }
    }

    private sealed class TrackingNormalizerReturningFailure : IActionIntentNormalizer
    {
        public ActionIntentResult Normalize(AgentAction action) =>
            ActionIntentResult.Unknown(action.RawAction);
    }

    private sealed class TrackingNormalizer : IActionIntentNormalizer
    {
        public ActionIntentResult Normalize(AgentAction action) =>
            ActionIntentResult.Success(new ActionIntent
            {
                Capability = ActionIntentKind.FileRead,
                Target = "/workspace/foo.txt"
            });
    }

    private sealed class TrackingTranslatorReturningFailure : ICapabilityTranslator
    {
        public CapabilityTranslationResult Translate(ActionIntent intent) =>
            CapabilityTranslationResult.Failure(new TranslationFailure("Unknown capability"));
    }

    private sealed class TrackingTranslator : ICapabilityTranslator
    {
        public CapabilityTranslationResult Translate(ActionIntent intent) =>
            CapabilityTranslationResult.Success(intent.Capability, intent.Target, intent.Metadata);
    }

    private sealed class TrackingEvaluatorReturningFailure : IPolicyEvaluator
    {
        public PolicyEvaluationResult Evaluate(CapabilityTranslationResult translation) =>
            PolicyEvaluationResult.Failure(new PolicyEvaluationFailure("Denied by policy"));
    }

    #endregion
}
