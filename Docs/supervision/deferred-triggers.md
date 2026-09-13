# Deferred Decisions Must Remain Trigger-Based

Before each module, resolve only decisions required by that module.

BEFORE Streaming: exact commit boundary; tool-call fragment buffering semantics.
BEFORE Policy interception: exact first deterministic rules; structural validation; bounded REPLAN budget.
BEFORE CompletionVerifier: claim detection boundary; evidence-to-status rules; client-facing correction behavior.
BEFORE Task Recovery: checkpoint triggers; handoff DTO; action uncertainty state model.
BEFORE advanced Agent Guard: hard execution-boundary integrations.

Do NOT resolve deferred details in this documentation pass.
