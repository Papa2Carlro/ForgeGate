# Policy Outcome / REPLAN Boundary

**Status:** Decided
**Date:** 2026-09-20
**Slice:** 28 post-implementation
**Refs:** `Docs/policy/outcomes.md`, `Docs/policy/correction-loop.md`, `Docs/policy/pre-execution.md`, `Docs/supervision/action-policy.md`, `Docs/policy/semantic-escalation.md`, `Docs/decisions/agent-guard-capability-translation.md`

---

## Context

Documentation references `REPLAN` as a canonical outcome alongside `ALLOW`, `DENY`, and `ASK_USER`. Runtime code implements only three `PolicyDecision` values: `Allow`, `Deny`, `RequireHumanApproval`. This created ambiguity about whether `REPLAN` should become a distinct runtime enum value or remain an orchestration concept.

## Decision

**`REPLAN` is NOT a runtime `PolicyDecision` value.**

Runtime policy model remains:

```
PolicyDecision
├── Allow
├── Deny
└── RequireHumanApproval
```

`REPLAN` is an **orchestration control flow** triggered after an eligible `DENY`; it is not a policy evaluation output.

### What this means

1. **`DENY`** = policy evaluator says "this candidate action cannot execute as-is"
2. **`REPLAN`** = orchestration control flow that MAY feed denial reason back to model for correction, bounded by retry budget. Exact eligibility conditions are deferred.
3. **Correction loop** = secondary mechanism that MAY be triggered by `DENY`; not all `DENY` outcomes necessarily trigger correction. Correction eligibility rules are deferred per `deferred-triggers.md`.
4. **Semantic reasoner** = Layer 4 of pipeline; its eventual output contract must remain compatible with the existing 3-value `PolicyDecision` model and must not require a new `Replan` outcome.

### Evidence basis

- ADR 0001 point 7: "Model correction ... is a secondary mechanism, used only when: the resulting capability was policy-denied"
- `Docs/policy/correction-loop.md`: flow is "policy denial → ForgeGate corrective feedback → same model replans"
- `Docs/policy/outcomes.md`: "DENY (action must not execute) and REPLAN (model should choose another compliant path) are different concepts" — these are SYSTEM-level behaviors, not policy-layer peers
- Current code: 0 references to `REPLAN` in `src/`; `PolicyDecision` has exactly 3 values

## Consequences

### For Policy Layer
- `PolicyDecision` enum unchanged — no `Replan` value added
- `PolicyEvaluationResult` unchanged — still carries `Decision` + `Reason`
- `AgentGuardResult` unchanged — still wraps `PolicyDecision` + semantic context
- Semantic reasoner must not require a new runtime `Replan` policy outcome; its eventual output contract remains to be defined and must remain compatible with this boundary unless a later ADR explicitly changes it

### For Orchestration Layer
- Correction loop is ORCHESTRATION concern, not policy concern
- `DENY` MAY become trigger for correction/replan flow; exact eligibility conditions are deferred
- Actionable feedback contract is deferred (no structured "DenialReason" field exists in current `ProviderFailure`)
- Budget management: bounded retry (exact bounds deferred per `deferred-triggers.md`)
- Client visibility: internal correction turns NOT exposed as ordinary output

### For Documentation
- `Docs/policy/outcomes.md`: "ALLOW / DENY / REPLAN / ASK_USER" should be interpreted as SYSTEM-LEVEL outcomes; REPLAN describes orchestration behavior after `DENY`, not a peer policy decision
- `Docs/policy/pre-execution.md`: "ALLOW or DENY/REPLAN/ASK_USER" — REPLAN is orchestration control flow after DENY, not a separate policy evaluation result
- `Docs/supervision/action-policy.md`: pipeline output is conceptually "ALLOW / DENY / ASK_USER"; REPLAN is correction loop behavior at the orchestration layer

### For ASK_USER / RequireHumanApproval
- `RequireHumanApproval` in code serves the role of `ASK_USER` in docs
- Naming difference is intentional (code is more descriptive)
- No behavioral difference evidenced
- Mapping is CONFIRMED by semantic alignment

### For Task-Level REPLAN
- `Docs/architecture/task/task-correction.md` describes REPLAN at TASK GOAL level (scope-drift correction)
- This is SEPARATE from policy-level correction loop
- No conflation: task-level REPLAN ≠ policy correction REPLAN

## Unresolved (Deferred)

This ADR does NOT resolve:

- `ActionIntent` schema (kind/target/mechanism/scope fields and types)
- Structural validation contract (what is validated, where, how)
- First deterministic rules (concrete allow/deny rule set)
- Suspicious/risk criteria (what triggers semantic escalation)
- Semantic escalation threshold (when to escalate)
- Correction eligibility rules (which DENYs trigger correction)
- Actionable feedback contract (structure of correction context)
- Max correction/replan rounds (budget)
- Timeout/circuit breaker conditions
- Semantic reasoner model/interface
- Client-facing representation of correction failure

These remain deferred per `Docs/supervision/deferred-triggers.md`.

## Slice 29 Status

**Slice 29: NOT YET DEFINED**

Its scope cannot yet be bounded because prerequisite contracts from `Docs/supervision/deferred-triggers.md` remain unresolved. The REPLAN policy-boundary question is resolved by this ADR. The next bounded research task must identify which remaining deferred contracts are prerequisites for semantic escalation and establish their dependency/order without implementing them.

## References

- `Docs/policy/outcomes.md` — conceptual outcomes
- `Docs/policy/correction-loop.md` — correction flow
- `Docs/policy/pre-execution.md` — enforcement order
- `Docs/supervision/action-policy.md` — layered pipeline
- `Docs/policy/semantic-escalation.md` — when semantic reasoner is used
- `Docs/decisions/agent-guard-capability-translation.md` — ADR 0001, correction as secondary mode
- `Docs/supervision/deferred-triggers.md` — trigger-based decision boundaries
