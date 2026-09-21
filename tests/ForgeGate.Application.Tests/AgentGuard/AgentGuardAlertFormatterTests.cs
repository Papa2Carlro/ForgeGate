using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

/// <summary>
/// Tests for AgentGuardAlertFormatter.
/// </summary>
public class AgentGuardAlertFormatterTests
{
    private readonly AgentGuardAlertFormatter _formatter = new();

    [Fact]
    public void Format_SuccessEvent_IncludeDecisionAndCapability()
    {
        var @event = CreateSuccessEvent(PolicyDecision.Allow, ActionIntentKind.FileRead, "/workspace/foo.txt");

        var alert = _formatter.Format(@event);

        Assert.Contains("Action approved", alert);
        Assert.Contains("FileRead", alert);
        Assert.Contains("/workspace/foo.txt", alert);
        Assert.Contains("Allow", alert);
        Assert.Contains(@event.EventId.ToString(), alert);
    }

    [Fact]
    public void Format_DenyEvent_IncludeDenyDecision()
    {
        var @event = CreateSuccessEvent(PolicyDecision.Deny, ActionIntentKind.FileDelete, "/workspace/important.txt");

        var alert = _formatter.Format(@event);

        Assert.Contains("Deny", alert);
        Assert.Contains("FileDelete", alert);
    }

    [Fact]
    public void Format_RequireHumanApprovalEvent_IncludeApprovalDecision()
    {
        var @event = CreateSuccessEvent(PolicyDecision.RequireHumanApproval, ActionIntentKind.ProcessSpawn, "/bin/rm");

        var alert = _formatter.Format(@event);

        Assert.Contains("RequireHumanApproval", alert);
    }

    [Fact]
    public void Format_WithLayer3Result_IncludeRiskFinding()
    {
        var @event = CreateSuccessEvent(PolicyDecision.Allow, ActionIntentKind.FileRead, "/workspace/foo.txt", Layer3EvaluationResult.RiskFound);

        var alert = _formatter.Format(@event);

        Assert.Contains("RISK DETECTED", alert);
    }

    [Fact]
    public void Format_WithoutLayer3Result_OmitsLayer3Line()
    {
        var @event = CreateSuccessEvent(PolicyDecision.Allow, ActionIntentKind.FileRead, "/workspace/foo.txt", null);

        var alert = _formatter.Format(@event);

        Assert.DoesNotContain("Layer 3", alert);
    }

    [Fact]
    public void Format_NormalizationFailedEvent_IncludeRawAction()
    {
        var @event = CreateFailureEvent(AgentGuardOutcomeType.NormalizationFailed, "Normalization", "Unknown command");

        var alert = _formatter.Format(@event);

        Assert.Contains("Normalization failed", alert);
        Assert.Contains("Unknown command", alert);
    }

    [Fact]
    public void Format_TranslationFailedEvent_IncludeCapability()
    {
        var @event = CreateFailureEvent(AgentGuardOutcomeType.TranslationFailed, "Translation", "Unknown capability");

        var alert = _formatter.Format(@event);

        Assert.Contains("Translation failed", alert);
        Assert.Contains("Unknown capability", alert);
    }

    [Fact]
    public void Format_PolicyEvaluationFailedEvent_IncludeReason()
    {
        var @event = CreateFailureEvent(AgentGuardOutcomeType.PolicyEvaluationFailed, "PolicyEvaluation", "Denied by policy");

        var alert = _formatter.Format(@event);

        Assert.Contains("Policy evaluation failed", alert);
        Assert.Contains("Denied by policy", alert);
    }

    [Fact]
    public void Format_IsDeterministic()
    {
        var @event = CreateSuccessEvent(PolicyDecision.Allow, ActionIntentKind.FileRead, "/workspace/foo.txt");

        var alert1 = _formatter.Format(@event);
        var alert2 = _formatter.Format(@event);

        Assert.Equal(alert1, alert2);
    }

    [Fact]
    public void Format_IncludeEventIdForTraceability()
    {
        var eventId = Guid.NewGuid();
        var @event = CreateSuccessEvent(PolicyDecision.Allow, ActionIntentKind.FileRead, "/workspace/foo.txt", eventId: eventId);

        var alert = _formatter.Format(@event);

        Assert.Contains(eventId.ToString(), alert);
    }

    #region Helpers

    private static AgentGuardEvent CreateSuccessEvent(
        PolicyDecision decision,
        ActionIntentKind capability,
        string target,
        Layer3EvaluationResult? layer3Result = null,
        Guid? eventId = null)
    {
        var observation = new AgentActionObservation
        {
            Source = new AgentAction { Source = "test", RawAction = "cat /workspace/foo.txt" },
            CorrelationId = Guid.NewGuid()
        };

        var result = AgentGuardResult.Success(decision, capability, target, layer3Result: layer3Result);
        var @event = AgentGuardEvent.FromSuccess(result, observation);

        if (eventId.HasValue)
            return new AgentGuardEvent
            {
                EventId = eventId.Value,
                Observation = @event.Observation,
                OutcomeType = @event.OutcomeType,
                Decision = @event.Decision,
                Capability = @event.Capability,
                Target = @event.Target,
                Metadata = @event.Metadata,
                Layer3Result = @event.Layer3Result,
                FailureStage = null,
                FailureReason = null,
                CreatedAt = @event.CreatedAt
            };

        return @event;
    }

    private static AgentGuardEvent CreateFailureEvent(
        AgentGuardOutcomeType outcomeType,
        string failureStage,
        string failureReason,
        Guid? eventId = null)
    {
        var observation = new AgentActionObservation
        {
            Source = new AgentAction { Source = "test", RawAction = "test action" },
            CorrelationId = Guid.NewGuid()
        };

        var @event = outcomeType switch
        {
            AgentGuardOutcomeType.NormalizationFailed => new AgentGuardEvent
            {
                EventId = eventId ?? Guid.NewGuid(),
                Observation = observation,
                OutcomeType = outcomeType,
                Decision = null,
                Capability = null,
                Target = null,
                Metadata = null,
                Layer3Result = null,
                FailureStage = failureStage,
                FailureReason = failureReason,
                CreatedAt = DateTime.UtcNow
            },
            AgentGuardOutcomeType.TranslationFailed => new AgentGuardEvent
            {
                EventId = eventId ?? Guid.NewGuid(),
                Observation = observation,
                OutcomeType = outcomeType,
                Decision = null,
                Capability = ActionIntentKind.Unknown,
                Target = null,
                Metadata = null,
                Layer3Result = null,
                FailureStage = failureStage,
                FailureReason = failureReason,
                CreatedAt = DateTime.UtcNow
            },
            AgentGuardOutcomeType.PolicyEvaluationFailed => new AgentGuardEvent
            {
                EventId = eventId ?? Guid.NewGuid(),
                Observation = observation,
                OutcomeType = outcomeType,
                Decision = null,
                Capability = ActionIntentKind.Unknown,
                Target = null,
                Metadata = null,
                Layer3Result = null,
                FailureStage = failureStage,
                FailureReason = failureReason,
                CreatedAt = DateTime.UtcNow
            },
            _ => throw new InvalidOperationException($"Unsupported outcome type: {outcomeType}")
        };

        return @event;
    }

    #endregion
}
