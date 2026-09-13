# Decisions Captured

- Hybrid direction decided; gateway-only now.
- OpenAI compatibility decided.
- Tool-call interception direction decided; not full execution ownership.
- Layered deterministic + semantic policy decided.
- Session/history-aware evaluation expected.
- Auditability first-class.

Not yet decided: session data model, persistence, exact policy DTOs, ActionIntent schema, reasoner model, correction-loop protocol, admin UI, provider routing strategy, Agent Guard protocol.

DECIDED (new):
- three model addressing modes; direct mode still passes policy/supervision.
- concurrent multi-session operation is baseline.
- adaptive routing with wait/degrade/ask/fail choices.
- user-visible material routing status.
- policy-driven adaptive autonomy.
- behavioral supervision as distinct concern.
- long-running command awareness required.

NOT YET DECIDED (new):
- exact routing scoring algorithm; exact quality score model; exact client capability protocol; exact process tracking model; exact detector thresholds; exact ASK_USER integration; exact concurrency scheduler design; exact quota accounting implementation.

DECIDED (new):
- single-instance concurrent multi-project/multi-session runtime; ephemeral in-process, durable in PostgreSQL.
- standard /v1/chat/completions public API unchanged.
- internal session/workspace resolution; session ≠ workspace.
- Workspace Coordination as distinct concern.
- soft ownership + escalation; cross-session protection.
- configurable model-specific supervision; hierarchical scopes.
- adaptive Task Manager; stricter when behavior degrades.
- progress supervision uses signals + task context, not one universal metric.

NOT YET DECIDED (new):
- exact session fingerprint algorithm; exact workspace detection; exact task-state DTO; exact supervision profile schema; exact config merge semantics; exact progress-scoring mechanism; exact workspace-attribution confidence model.

No contradictions found in existing repo (only gateway scaffold present; no Agent Guard runtime; no premature policy schema locked).
