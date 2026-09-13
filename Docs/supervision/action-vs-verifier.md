# Action Policy vs Completion Verifier

Distinct concerns.

Action Policy: "May this proposed action execute?"
Completion Verifier: "Is this completion/status claim supported by available evidence?"

Examples: git reset --hard → Action Policy. "all tests pass" → Completion Verifier. Do NOT collapse into one monolithic PolicyEngine. May share evidence/activity/task context.
