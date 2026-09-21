using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Default alert formatter for Agent Guard events.
/// 
/// Produces deterministic, human-readable alert strings from structured events.
/// Output is traceable via EventId and includes the relevant outcome details.
/// 
/// Success events: show capability, target, decision, and Layer 3 finding.
/// Failure events: show failure stage and reason.
/// </summary>
public sealed class AgentGuardAlertFormatter : IAlertFormatter
{
    public string Format(AgentGuardEvent @event)
    {
        return @event.OutcomeType switch
        {
            AgentGuardOutcomeType.PolicyEvaluated => FormatSuccess(@event),
            AgentGuardOutcomeType.NormalizationFailed => $"[Agent Guard] Normalization failed for action '{@event.Observation.Source.RawAction}' — {@event.FailureReason}",
            AgentGuardOutcomeType.TranslationFailed => $"[Agent Guard] Translation failed for capability '{@event.Capability}' — {@event.FailureReason}",
            AgentGuardOutcomeType.PolicyEvaluationFailed => $"[Agent Guard] Policy evaluation failed for capability '{@event.Capability}' — {@event.FailureReason}",
            _ => $"[Agent Guard] Event {@event.EventId}: unknown outcome type {@event.OutcomeType}"
        };
    }

    private static string FormatSuccess(AgentGuardEvent @event)
    {
        var decision = @event.Decision?.ToString() ?? "Unknown";
        var capability = @event.Capability?.ToString() ?? "Unknown";
        var target = string.IsNullOrEmpty(@event.Target) ? "(no target)" : @event.Target;
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Agent Guard] Action approved: {capability} {target} → {decision}");
        sb.AppendLine($"  EventId: {@event.EventId}");
        sb.AppendLine($"  Decision: {decision}");
        
        if (@event.Layer3Result != null)
        {
            var riskStatus = @event.Layer3Result.HasRiskFinding ? "RISK DETECTED" : "No risk found";
            sb.AppendLine($"  Layer 3: {riskStatus}");
        }
        
        return sb.ToString().TrimEnd();
    }
}
