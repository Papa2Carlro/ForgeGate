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

No contradictions found in existing repo (only gateway scaffold present; no Agent Guard runtime; no premature policy schema locked).
