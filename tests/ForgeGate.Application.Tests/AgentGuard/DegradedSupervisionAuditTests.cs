using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

/// <summary>
/// Tests for degraded supervision audit events (Slice 32).
/// </summary>
public class DegradedSupervisionAuditTests
{
    [Fact]
    public void FromDegradedSupervision_CreatesEventWithCorrectOutcomeType()
    {
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen");

        Assert.Equal(AgentGuardOutcomeType.DegradedSupervision, @event.OutcomeType);
    }

    [Fact]
    public void FromDegradedSupervision_PreservesRequiredFields()
    {
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Deny,
            capability: ActionIntentKind.FileWrite,
            target: "/workspace/production/code.py",
            degradedCondition: "DeniedUnderFailClosed",
            degradationReason: "Semantic supervisor unavailable");

        Assert.Equal(PolicyDecision.Deny, @event.Decision);
        Assert.Equal(ActionIntentKind.FileWrite, @event.Capability);
        Assert.Equal("/workspace/production/code.py", @event.Target);
        Assert.Equal("DeniedUnderFailClosed", @event.DegradedCondition);
        Assert.Equal("Semantic supervisor unavailable", @event.DegradationReason);
    }

    [Fact]
    public void FromDegradedSupervision_AllowedUnderFailOpen_CorrectCondition()
    {
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/readme.md",
            degradedCondition: "AllowedUnderFailOpen");

        Assert.Equal("AllowedUnderFailOpen", @event.DegradedCondition);
        Assert.Equal(PolicyDecision.Allow, @event.Decision);
    }

    [Fact]
    public void FromDegradedSupervision_DeniedUnderFailClosed_CorrectCondition()
    {
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Deny,
            capability: ActionIntentKind.ProcessSpawn,
            target: "/bin/rm -rf /",
            degradedCondition: "DeniedUnderFailClosed");

        Assert.Equal("DeniedUnderFailClosed", @event.DegradedCondition);
        Assert.Equal(PolicyDecision.Deny, @event.Decision);
    }

    [Fact]
    public void FromDegradedSupervision_SemanticPolicyUnavailable_CorrectCondition()
    {
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.RequireHumanApproval,
            capability: ActionIntentKind.FileWrite,
            target: "/workspace/src/main.py",
            degradedCondition: "SemanticPolicyUnavailable");

        Assert.Equal("SemanticPolicyUnavailable", @event.DegradedCondition);
        Assert.Equal(PolicyDecision.RequireHumanApproval, @event.Decision);
    }

    [Fact]
    public void FromDegradedSupervision_OptionalDegradationReason_DefaultsNull()
    {
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen");

        Assert.Null(@event.DegradationReason);
    }

    [Fact]
    public void FromDegradedSupervision_OptionalLayer3Result_Preserved()
    {
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen",
            layer3Result: Layer3EvaluationResult.RiskFound);

        Assert.NotNull(@event.Layer3Result);
        Assert.True(@event.Layer3Result.HasRiskFinding);
    }

    [Fact]
    public void FromDegradedSupervision_GeneratesEventId()
    {
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen");

        Assert.NotEqual(Guid.Empty, @event.EventId);
    }

    [Fact]
    public void FromDegradedSupervision_SetsCreatedAt()
    {
        var before = DateTime.UtcNow;
        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen");
        var after = DateTime.UtcNow;

        Assert.InRange(@event.CreatedAt, before, after);
    }

    [Fact]
    public void FromDegradedSupervision_PreservesObservation()
    {
        var observation = new AgentActionObservation
        {
            Source = new AgentAction { Source = "test", RawAction = "cat /workspace/foo.txt" },
            CorrelationId = new Guid("12345678-1234-5678-1234-567812345678")
        };

        var @event = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen",
            observation: observation);

        Assert.Equal(observation.CorrelationId, @event.Observation.CorrelationId);
        Assert.Equal("test", @event.Observation.Source.Source);
    }

    [Fact]
    public void FromDegradedSupervision_DoesNotAffectPolicyDecision()
    {
        var allowEvent = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen");

        var denyEvent = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Deny,
            capability: ActionIntentKind.FileDelete,
            target: "/workspace/important.txt",
            degradedCondition: "DeniedUnderFailClosed");

        // Events are independent — creating one does not affect the other
        Assert.Equal(PolicyDecision.Allow, allowEvent.Decision);
        Assert.Equal(PolicyDecision.Deny, denyEvent.Decision);
    }

    [Fact]
    public void FromDegradedSupervision_IsDeterministic()
    {
        var event1 = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen",
            degradationReason: "Semantic timeout");

        var event2 = AgentGuardEvent.FromDegradedSupervision(
            decision: PolicyDecision.Allow,
            capability: ActionIntentKind.FileRead,
            target: "/workspace/foo.txt",
            degradedCondition: "AllowedUnderFailOpen",
            degradationReason: "Semantic timeout");

        // Same inputs produce same semantic content (EventId differs)
        Assert.Equal(event1.OutcomeType, event2.OutcomeType);
        Assert.Equal(event1.Decision, event2.Decision);
        Assert.Equal(event1.Capability, event2.Capability);
        Assert.Equal(event1.Target, event2.Target);
        Assert.Equal(event1.DegradedCondition, event2.DegradedCondition);
        Assert.Equal(event1.DegradationReason, event2.DegradationReason);
    }
}
