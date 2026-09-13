# Application Error / Outcome Model

Hybrid typed-outcome model. Expected business/orchestration conditions use explicit typed results/states. Unexpected technical faults use exceptions. Potential concepts: RouteSelectionResult; CapacityAcquireResult; PolicyEvaluationResult; CompletionAssessment. Not finalized. Expected outcomes (no eligible route; wait required; policy denied; quota exhausted; completion not proven) must not require exception-driven normal control flow.
