# Completion Verification Strategy

Deterministic evidence checks first → semantic Completion Verifier only when ambiguity remains.

Example: claim "Runtime PASS" + known facts (build passed, runtime never executed) → claim cannot be certified; correction to NOT_RUN / NOT_PROVEN.

Example: claim "BLOCKED" + no execution attempt → invalid; BLOCKED must not replace NOT_RUN.
