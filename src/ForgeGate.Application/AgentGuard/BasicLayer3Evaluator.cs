using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.AgentGuard;

/// <summary>
/// Default Layer 3 evaluator — baseline implementation.
/// 
/// This evaluator performs bounded risk/suspicion evaluation.
/// For MVP, it uses a simple heuristic based on capability metadata.
/// 
/// Future implementations may add cross-session protection,
/// confidence modeling, and richer risk indicators.
/// </summary>
public sealed class BasicLayer3Evaluator : ILayer3Evaluator
{
    public Layer3EvaluationResult Evaluate(ActionIntentKind capability, string? contextualEvidence = null)
    {
        // MVP: baseline evaluation returns NoRiskFound.
        // Future slices may add cross-session risk detection here.
        return Layer3EvaluationResult.NoRiskFound;
    }
}
