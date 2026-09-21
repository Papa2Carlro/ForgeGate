# Layer 3 Cross-Session Attribution Contract

**Status:** Decided
**Date:** 2026-09-21
**Slice:** Pre-Slice-29 (prerequisite for semantic escalation bounded research)
**Refs:** `Docs/decisions/agent-guard-capability-translation.md`, `Docs/decisions/policy-outcome-replan-boundary.md`, `Docs/architecture/supervision/cross-session.md`, `Docs/architecture/supervision/soft-ownership.md`, `Docs/policy/semantic-escalation.md`, `Docs/supervision/deferred-triggers.md`

---

## Context

Layer 3 of the Agent Guard pipeline is the cross-session semantic evidence layer. It provides the contextual attribution signals that the deterministic policy layer and the eventual semantic reasoner consume.

Relevant established decisions:

- **Context Ownership = Option B** (`Docs/decisions/agent-guard-capability-translation.md`, Status: Locked): caller/orchestration layer supplies contextual evidence; Agent Guard does not resolve contextual infrastructure itself.
- **REPLAN ≠ runtime `PolicyDecision`** (`Docs/decisions/policy-outcome-replan-boundary.md`, Status: Decided): runtime `PolicyDecision` remains `Allow | Deny | RequireHumanApproval`; REPLAN is orchestration control flow, not a policy outcome.
- **Basic cross-session protection is MVP scope** (`Docs/policy/mvp-scope.md`): included in MVP priorities alongside structural validation and deterministic safety rules.
- **Soft ownership uses continuous signals** (`Docs/architecture/supervision/soft-ownership.md`): path attribution includes "recently touched by session, active mutation intent, confidence/recency" — signals, not discrete states.
- **Semantic escalation handles ambiguous cross-session mutation** (`Docs/policy/semantic-escalation.md`): semantic reasoner is used only when deterministic rules cannot safely resolve intent/proportionality; examples include "ambiguous cross-session mutation."

This decision establishes the minimum semantic contract for Layer 3 evidence representation. It does not define the runtime API, the signal resolution mechanism, or the semantic reasoner contract.

---

## Decision A — ForeignSessionId

**`ForeignSessionId` is not required by the minimum Layer 3 cross-session semantic evidence contract.**

Under Option B (Context Ownership), the caller/orchestration layer may resolve session identity and pass it as contextual enrichment to the Layer 3 gate. This decision establishes that a specific `ForeignSessionId` is **not a prerequisite** of the minimum semantic evidence contract.

The minimum contract requires only that the gate can evaluate whether foreign attribution is present or absent — it does not require a specific foreign-session identifier field.

### Scope

- Caller/orchestration **may** provide `ForeignSessionId` as optional enrichment when session identity is resolved.
- `ForeignSessionId` is **not a prerequisite** of the minimum Layer 3 semantic evidence contract.
- This decision does **not** prohibit future use of specific session identity in enrichment layers above the minimum.
- This decision does **not** assert that session identity is unnecessary for cross-session evaluation — it only establishes that the minimum contract does not require a specific `ForeignSessionId` field. Orchestration-side attribution determination may still depend on session identity resolution.
- How orchestration resolves foreign session identity is deferred (`SessionResolver` API).

### What this does NOT decide

- The format or type of `ForeignSessionId` when present (implementation decision).
- Whether absence of `ForeignSessionId` affects risk scoring (deferred: risk score threshold).
- The `SessionResolver` API or its integration points (deferred).

---

## Decision B — CurrentSessionAttribution

**`CurrentSessionAttribution` is not a distinct required semantic state in the minimum Layer 3 attribution contract.**

The minimum contract distinguishes only:

1. No relevant foreign attribution detected.
2. Foreign attribution detected.
3. Attribution unavailable.

Current-session activity is the implicit baseline against which "foreign" is determined — it does not require its own state in the minimum representation.
contexts or future models. It asserts only that the **minimum contract** does not require a separate discrete state to represent current-session activity.

The exact attribution-state taxonomy remains open. Current-session activity may be modeled differently in future enrichment layers, and the semantics distinguishing each state (including `Unavailable` vs. `NoRelevantAttribution`) are deferred.

### Scope

- The minimum contract does not require a discrete `CurrentSessionAttribution` state.
- Enrichment layers may distinguish current-session activity from idle when useful for downstream purposes (UI, audit, correlation).
- Active-session lifecycle (when attribution transitions between states) remains deferred.
- Exact attribution-state taxonomy remains deferred — this decision constrains the minimum contract but does not lock the final state model.

### What this does NOT decide

- Whether current-session activity appears in optional enrichment fields (deferred).
- The active-session lifecycle model (deferred).
- The exact semantics distinguishing states in the attribution model (deferred).
- The final canonical state taxonomy (deferred to a future bounded decision
- Whether current-session activity appears in optional enrichment fields (deferred).
- The active-session lifecycle model (deferred).
- The exact semantics distinguishing `Unavailable` from `NoRelevantAttribution` (deferred).

---

## Resulting Minimum Semantic Model

Decisions A and B constrain the minimum Layer 3 attribution contract but do **not** establish a canonical state taxonomy.

An illustrative model consistent with these decisions would distinguish:

```
Illustrative model (PROPOSED, not canonical):
├── NoRelevantAttribution   — no foreign attribution signal detected (covers absence of foreign attribution and current-session baseline)
├── ForeignAttribution      — foreign attribution signal detected
└── Unavailable             — attribution signal cannot be determined
```

**These state names are PROPOSED, not yet canonical.** They illustrate one possible minimum model derived from Decisions A and B but have **not** been adopted as formal terminology. A future bounded decision may rename, extend, replace, or otherwise refine this taxonomy. This ADR does not create a runtime enum or immutable state model.

**`ForeignSessionId`:** not required by the minimum contract. Optional enrichment only.

**`CurrentSessionAttribution`:** not a separate minimum state. At the minimum level, current-session activity is subsumed under the "no relevant foreign attribution" condition — it is the implicit baseline, not a distinct state.

---

## Explicitly Deferred

The following questions remain open and are intentionally NOT resolved by this decision:

- Recency representation (timestamp vs. sequence number vs. normalized score)
- Confidence representation (binary vs. continuous vs. tiered)
- Active-session lifecycle (state transition triggers)
- Mutation-intent semantics (what constitutes "active mutation intent")
- `SessionResolver` API design and integration
- Exact Layer 3 evidence API contract (fields, serialization, validation)
- Risk scoring and threshold values
- Semantic escalation threshold (when to escalate to semantic reasoner)
- Semantic reasoner contract
- Correction-loop eligibility and budget
- `Unavailable` failure behavior (retry? escalate? deny-by-default?)

These are deferred per `Docs/supervision/deferred-triggers.md`.

---

## Consequences

### For Layer 3 Contract
- Minimum evidence contract does not require `ForeignSessionId`
- Minimum contract does not require `CurrentSessionAttribution` as a discrete state
- Orchestration layer retains freedom to supply session identity as optional enrichment

### For Implementation
- No source code changes required by this decision
- No runtime enum or type creation required
- Attribution-state taxonomy (exact state names, transitions, semantics) remains deferred to a future bounded decision

### For Boundary Preservation
- Option B context ownership boundary preserved: Agent Guard receives evidence from orchestration without resolving session identity itself
- REPLAN boundary preserved: this decision governs data contract, not control flow
- Basic cross-session protection remains in MVP scope per `Docs/policy/mvp-scope.md`

### For Deferred Work
- All deferred questions (recency, confidence, lifecycle, risk scoring, semantic reasoner, correction loop, etc.) remain open per `Docs/supervision/deferred-triggers.md`
