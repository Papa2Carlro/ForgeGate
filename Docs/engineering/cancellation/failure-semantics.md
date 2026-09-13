# Cancellation Failure Semantics

User/client cancellation should not normally be reported as internal system failure. Provider timeout normalizes to provider failure model. Semantic policy/verifier timeout represents supervision degradation, not provider failure. Do NOT collapse every OperationCanceledException into one generic error.
