using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Contract for the intent normalization stage of Agent Guard.
/// 
/// Responsibility: translate a raw AgentAction into a canonical
/// ActionIntent (or acknowledge Unknown), without making any
/// policy or execution decisions.
/// 
/// This is a pure function contract — the normalizer must not:
/// - Execute the action
/// - Consult policy
/// - Mutate state beyond its own normalization buffer
/// </summary>
public interface IActionIntentNormalizer
{
    /// <summary>
    /// Normalizes a raw agent action into a semantic intent.
    /// Returns Success with ActionIntent, or Unknown with failure reason.
    /// </summary>
    ActionIntentResult Normalize(AgentAction action);
}
