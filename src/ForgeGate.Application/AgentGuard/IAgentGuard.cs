using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Contract for the Agent Guard enforcement boundary.
/// 
/// This is the top-level application service that coordinates the
/// complete Agent Guard pipeline:
/// 
///     AgentAction
///         ↓
///     Normalization (IActionIntentNormalizer)
///         ↓
///     Translation (ICapabilityTranslator)
///         ↓
///     Policy Evaluation (IPolicyEvaluator)
///         ↓
///     AgentGuardResult
/// 
/// The caller receives one explicit result describing the complete
/// enforcement outcome. The caller does NOT need to manually coordinate
/// the individual stages.
/// 
/// This service does NOT:
/// - Execute capabilities
/// - Persist events
/// - Send alerts
/// - Integrate with the gateway
/// 
/// See: Docs/decisions/agent-guard-capability-translation.md
/// </summary>
public interface IAgentGuard
{
    /// <summary>
    /// Evaluates an agent action through the complete Agent Guard pipeline.
    /// Returns an explicit enforcement outcome.
    /// </summary>
    AgentGuardResult Evaluate(AgentAction action);
}
