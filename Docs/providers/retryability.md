# Retryability and Failure Scope

Category alone insufficient. Must understand: immediately retryable; retryable after delay; retryable through another provider; not retryable without config/user intervention.

Examples: temporary route rate limit → WAIT/switch provider. Daily credential quota exhausted → switch, not retry every few seconds. Invalid model ID → route/config problem, no retry loop. Auth failure → admin/config problem. Context exceeded → another route only if sufficient context.
