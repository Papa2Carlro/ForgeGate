# Layer 3 Evaluation Result Boundary

**Status:** Decided
**Date:** 2026-09-21
**Slice:** Pre-Slice-29 (follows `layer-3-attribution-contract.md`)
**Refs:** `Docs/decisions/layer-3-attribution-contract.md`, `Docs/decisions/agent-guard-capability-translation.md`, `Docs/decisions/policy-outcome-replan-boundary.md`, `Docs/supervision/action-policy.md`, `Docs/policy/tool-enforcement.md`, `Docs/policy/semantic-escalation.md`

---

## Decision

**Layer 3 Evaluation Result communicates the finding of the Layer 3 risk/suspicion evaluation and MUST semantically distinguish whether a relevant risk/suspicion finding was present or absent.** It does not communicate an escalation command, Layer 4 invocation decision, or PolicyDecision. Orchestration interprets the evaluation finding and decides whether Layer 4 semantic reasoning is required.

This establishes the semantic category and minimum semantic distinction of what crosses the Layer 3 → orchestration boundary, without defining concrete representation.

---

## Pipeline Position

The canonical pipeline, as established by existing decisions, is:

```
Contextual Evidence (supplied by orchestration per Option B)
        ↓
Layer 3: Risk/Suspicion Evaluation
        ↓
Layer 3 Evaluation Result (evaluation finding)
        ↓
Orchestration: Interprets result, decides escalation
        ↓
Layer 4: Semantic Reasoner (invoked only when orchestration decides required)
        ↓
Policy Decision (Allow | Deny | RequireHumanApproval)
        ↓
REPLAN / ASK_USER (orchestration control flow)
```

---

## Responsibility Assignment

### Layer 3 owns:
- Risk/suspicion evaluation of candidate actions
- Producing the evaluation finding
- Operating within gate-only boundary

### Layer 3 does NOT own:
- Layer 4 invocation (orchestration decides)
- PolicyDecision production
- REPLAN production
- ASK_USER production
- Direct execution or blocking
- Contextual infrastructure resolution
- Session identity resolution

### Orchestration owns:
- Supplying contextual evidence (Option B)
- Interpreting the Layer 3 evaluation finding
- Deciding whether Layer 4 semantic reasoning is required
- Subsequent orchestration control flow

### Layer 4 owns:
- Semantic reasoning after orchestration decides Layer 4 evaluation is required

---

## Critical Distinctions

### "Evaluation finding" ≠ "Escalation decision"

Layer 3 communicates **what its risk/suspicion evaluation found**, including whether a relevant risk/suspicion finding was present or absent.

Orchestration decides **what to do with that finding**.

These are separate responsibilities. Layer 3 does not encode an escalation directive.

### Minimum semantic distinction is closed; representation is open

The decision establishes that the Layer 3 Evaluation Result MUST semantically distinguish between:
- a relevant risk/suspicion finding being present, and
- no relevant risk/suspicion finding being present.

This closes the **minimum semantic distinction** of the evaluation finding. It does NOT define how this distinction is represented (no taxonomy, no boolean, no enum, no DTO). Concrete representation remains open.

### "Suspicion/risk signal" ≠ Predefined taxonomy

Existing documentation references a "suspicion/risk signal" as a pipeline concept. This decision does not define:
- The signal's internal structure
- Any enum values (low/medium/high, clear/suspicious, etc.)
- Numeric scores
- Thresholds
- Confidence values

These remain deferred to future bounded decisions.

### Semantic category ≠ Concrete contract

This decision closes the **semantic category** (evaluation finding), not the **concrete representation**. The exact fields, types, and serialization of the evaluation result are deferred.

---

## Compatibility with Existing Decisions

| Existing Decision | Compatibility | Evidence |
|-------------------|---------------|----------|
| Context Ownership = Option B | ✅ Compatible | Layer 3 receives evidence from orchestration; does not resolve infrastructure |
| Layer 3 gate-only boundary | ✅ Compatible | Layer 3 remains purely evaluative; no enforcement action |
| REPLAN ≠ PolicyDecision | ✅ Compatible | Layer 3 does not produce REPLAN; orchestration controls flow |
| PolicyDecision = Allow\|Deny\|RequireHumanApproval | ✅ Compatible | No Layer 3 output changes the 3-value enum |
| MVP cross-session protection | ✅ Compatible | Contextual cross-session evidence may participate in Layer 3 risk/suspicion evaluation; exact attribution-to-evaluation mapping remains deferred |
| Semantic escalation deferred | ✅ Compatible | Threshold/criteria for escalation remain deferred |

---

## Explicitly Deferred

The following items remain OPEN and are intentionally NOT resolved by this decision:

- Exact semantic contents of the evaluation finding (what fields/data it contains)
- Concrete result representation (DTO, class, interface, enum, boolean)
- Risk/suspicion taxonomy (if any classification is used)
- Risk score representation
- Escalation threshold values
- Confidence representation
- Reason/actionable feedback structure
- Attribution state taxonomy (`NoRelevantAttribution` / `ForeignAttribution` / `Unavailable` are PROPOSED, not canonical)
- Unavailable/failure semantics
- Exact Layer 3 API contract
- Layer 4 contract
- Policy mapping (how evaluation finding maps to Allow/Deny/RequireHumanApproval)
- Correction-loop semantics

These remain deferred per `Docs/supervision/deferred-triggers.md` and `Docs/decisions/layer-3-attribution-contract.md`.

---

## Consequences

### For Layer 3 Contract
- Layer 3 produces an evaluation finding that semantically distinguishes presence/absence of risk/suspicion
- Orchestration retains full responsibility for interpretation and escalation decision
- Gate-only boundary is preserved: Layer 3 evaluates, does not enforce
- The minimum semantic distinction (present/absent) is now closed
- Concrete representation of the distinction remains open

### For Implementation
- No source code changes required by this decision
- No runtime enum or type creation required
- Future bounded decisions will define concrete representation

### For Boundary Preservation
- Option B context ownership preserved: orchestration supplies evidence, interprets results
- REPLAN boundary preserved: orchestration controls flow
- PolicyDecision boundary preserved: Layer 3 output does not alter enum

### For Future Work
- Slice 29 scope now has a defined responsibility boundary AND minimum semantic distinction
- The responsibility boundary, semantic category, and minimum semantic distinction are decided
- Exact semantic content beyond the present/absent distinction, concrete representation, and all other deferred items remain open for bounded contract research


