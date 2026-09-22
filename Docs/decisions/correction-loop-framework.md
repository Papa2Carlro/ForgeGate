# Correction Loop Framework — Owner Decision

**Status:** Decided for V0.1
**Date:** 2026-09-22
**Slice:** 33
**Refs:** `Docs/policy/correction-loop.md`, `Docs/decisions/agent-guard-capability-translation.md` §7, `Docs/decisions/policy-outcome-replan-boundary.md`, `Docs/supervision/deferred-triggers.md`, `Docs/decisions/backlog.md`, `Docs/decisions/layer-3-evaluation-result-boundary.md`

---

## Context

Slice 33 is blocked by three unresolved owner decisions explicitly referenced in the milestone roadmap:
- Correction prompt design
- Retry count / budget
- Eligibility conditions

These are listed in `Docs/decisions/backlog.md` under "BEFORE Agent Guard runtime" and in `Docs/supervision/deferred-triggers.md` under "bounded REPLAN flow" and "correction loop bounds."

The existing canonical boundary (`policy-outcome-replan-boundary.md`) establishes that:
- `REPLAN` is NOT a `PolicyDecision` value — it is an orchestration control flow.
- `DENY` may become a trigger for correction/replan flow; exact eligibility conditions are deferred.
- Budget management is bounded retry; exact bounds are deferred per `deferred-triggers.md`.

This decision partially resolves the three blocked questions at the level required for Slice 33 stub framework, without implementing the correction loop or changing any runtime contract.

**Critical caveat:** The numeric retry budget remains explicitly deferred and **blocks Slice 33 runtime implementation**. The roadmap at `Docs/planning/milestone-gateway-agent-guard.md` lists "Retry count/budget" as a required blocker. Because no canonical source specifies a concrete number and empirical input is required (per `deferred-triggers.md` and `backlog.md`), **Slice 33 cannot proceed to functional implementation until the numeric budget is decided**. The owner decision here defines the *framework* for budget management (where it lives, how it is consumed, what exhaustion means) but cannot prescribe the value itself.

A parameterized stub (budget as configurable input, no default) CAN be built now. A production-ready implementation CANNOT.

### What this decision resolves
- Eligibility conditions (Decision C) — fully resolved.
- Correction prompt contract scope (Decision A) — scope boundaries resolved; precise DTO deferred.
- Budget management semantics (Decision B) — structure and semantics resolved; numeric value deferred.

### What this decision does NOT resolve
- Numeric budget values (`recommended_min`, `recommended_max`, `default`) — all TBD, require empirical input. **[BLOCKS SLICE 33 RUNTIME]**
- Corrective feedback DTO / interface contract.
- Client-facing exhaustion behavior.
- Timeout / circuit breaker conditions.

---

## Status Summary

| Item | Status |
|------|--------|
| Eligibility matrix | **RESOLVED** — canonical + owner decision |
| Correction prompt scope (what data in/out) | **PARTIALLY RESOLVED** — scope boundaries set; exact DTO deferred per `deferred-triggers.md` |
| Budget semantics (where, when consumed, exhaustion) | **RESOLVED** — structure defined; numeric value deferred |
| Numeric retry budget model | **PARTIALLY RESOLVED** — framework defined; numeric values TBD |
| `recommended_min` / `recommended_max` / `default` | **DEFERRED — BLOCKS SLICE 33** — requires empirical input |
| Full pipeline re-entry | **DEFAULT ASSUMPTION** — inferred from `correction-loop.md` flow |
| Implementation boundary | **RESOLVED** — orchestration-layer stub only; no runtime contract changes |

**Slice 33 status: BLOCKED on numeric retry budget values.**
- Stub framework (parameterized, no default) can be designed: eligibility gating + configurable budget parameter + recommended-range model.
- Production implementation requires an owner decision specifying `recommended_min`, `recommended_max`, and `default` values before it can proceed.

### What is a correction request

A correction request is structured feedback produced by the orchestration layer after a `DENY` outcome from a **successfully translated** candidate action. It is NOT produced for:
- `RequireHumanApproval` (human-in-the-loop step, not a replanning signal)
- `Allow` (action proceeds; no correction needed)
- Normalization or translation failures (the model never reached policy evaluation)
- Layer 3 findings (evaluation finding ≠ denial; per `layer-3-evaluation-result-boundary.md`)
- Degraded supervision events (audit observation only; per `degraded-audit.md`)

**Evidence:** `agent-guard-capability-translation.md` §7: "Model correction … is a secondary mechanism, used only when: the translation succeeded but the resulting capability was policy-denied; The denial reason is actionable feedback the model can incorporate."

### Who formulates it

The orchestration layer forms the correction request. Agent Guard does NOT produce it — it only produces `AgentGuardResult` with `Decision` and `Reason`. The `Reason` field from `PolicyEvaluationResult` is the only correction-eligible data originating from Agent Guard.

**Evidence:** `policy-outcome-replan-boundary.md`: "Correction loop is ORCHESTRATION concern, not policy concern."

### Data the correction loop may pass back to the model

A correction request contains:
1. The raw agent action that was evaluated (from `AgentActionObservation.Source.RawAction`).
2. The denial reason from the policy evaluation (`PolicyEvaluationResult.Reason`).and whether it is encoded in the DTO or passed via a separate field is **deferred per `deferred-triggers.md`**
3. The normalized capability that was denied (`AgentGuardResult.Capability` / `ActionIntentKind`).
4. The target path/resource (`AgentGuardResult.Target`).

The correction request MUST NOT contain:
- Any `PolicyDecision` value other than those already in the canonical 3-value enum (Allow, Deny, RequireHumanApproval).
- A fabricated or inferred "suggested fix" — the model must generate the correction itself.
- Structured instructions that bypass the policy layer.

### Whether previous policy failures / findings are included

Previous correction-round outcomes (i.e., that this is round N of M) may be included in the feedback context, enabling the model to avoid repeating the same denied action. The exact mechanism is deferred per `deferred-triggers.md` ("correction loop bounds").

The `Layer3EvaluationResult` from the current evaluation is NOT fed back as part of the correction prompt unless the orchestration layer explicitly decides to include it — the Layer 3 result is an evaluation finding, not a denial reason (per `layer-3-evaluation-result-boundary.md`).

### Whether the specific rejection reason is included

**Yes.** The denial reason (`PolicyEvaluationResult.Reason`) is explicitly identified in `agent-guard-capability-translation.md` §7 as a prerequisite: "The denial reason is actionable feedback the model can incorporate." Without the reason, the model cannot replan intelligently.

### What the correction may change

The correction is fed back to the **same model** for replanning. The model may change:
- Tool arguments (e.g., different path, different flags)
- Tool selection (e.g., choose a different capability)
- Intent scope (narrower or differently scoped request)

The correction MUST NOT change:
- The `PolicyDecision` enum (still Allow/Deny/RequireHumanApproval).
- The layer ordering (normalization → translation → policy evaluation still applies to every round).

**Deferred:** Whether the correction may also change the `ActionIntent` kind/target at the structural level is deferred per `backlog.md` ("ActionIntent schema").

### What data is categorically prohibited from being added without a separate decision

- Any new `PolicyDecision` enum value.
- Any field that implies a correction loop is automatic for `RequireHumanApproval`.
- Any persistence/schema for correction rounds (Slice 35).
- Any UI/alert semantics for correction rounds (Slice 31 consumer).

---

## Decision B — Retry Budget Model

### Canonical "bounded" semantics

The word "bounded" in canonical docs means **finite** (not infinite). It prohibits unbounded retry loops — it does not prescribe a specific upper limit or hard safety ceiling.

**Evidence:**
- `correction-loop.md`: "Retry/replan loops bounded." — prohibits infinite loops, does not specify a number.
- `agent-guard-capability-translation.md` §7: "A bounded retry budget permits it." — finite budget required, value unspecified.
- `deferred-triggers.md`: "bounded REPLAN budget" — finite, not quantified.
- `backlog.md`: "correction loop bounds" — bounds required as a concept, not as a numeric value.
- `policy-outcome-replan-boundary.md`: "Budget management: bounded retry (exact bounds deferred per `deferred-triggers.md`)." — explicitly defers the numeric value.

**No canonical source states that "bounded" implies a hard maximum enforced by the system.** "Bounded" simply means the loop must terminate — it does not constrain whether the bound is:
- a fixed constant in code,
- a configurable parameter provided by the caller,
- or a recommended range with a separate configured actual value.

Therefore, a model with **recommended guidance + configurable actual budget** is canonical-compatible, provided the configured budget is always finite.

---

### Unit of retry (Owner Decision)

One correction round is defined as: orchestration feeds correction request → model replans → candidate re-enters the Agent Guard pipeline → new `AgentGuardResult` produced.

**Canonical evidence:** `correction-loop.md` states "Retry/replan loops bounded" without defining the unit. This decision establishes the unit for Slice 33 stub framework purposes.

**Deferred:** Whether a "round" includes the initial (non-corrected) evaluation or only corrected attempts.

---

### Where the budget lives

The retry budget lives in the **orchestration layer**, not in Agent Guard. Agent Guard remains stateless per each `Evaluate()` call. The orchestrator tracks how many correction rounds have been attempted for a given candidate action and compares against the budget.

**Evidence:** `policy-outcome-replan-boundary.md`: "Budget management: bounded retry (exact bounds deferred per `deferred-triggers.md`)." "Correction loop is ORCHESTRATION concern, not policy concern."

### Numeric budget model (Owner Decision)

The canonical docs require a finite budget but do not prescribe its structure. This decision establishes a **recommended-range model** with four tiers:

| Tier | Role | Status |
|------|------|--------|
| `recommended_min` | Operational guidance: minimum rounds recommended for typical deployments | TBD — requires empirical input |
| `recommended_max` | Operational guidance: maximum rounds recommended for typical deployments | TBD — requires empirical input |
| `default` | Value used when orchestration provides no explicit configuration | TBD — requires empirical input |
| `configured_budget` | Actual finite budget enforced at runtime by the orchestration | Provided by caller; must be a finite positive integer |

**Key semantic constraints:**

- `configured_budget` **is the runtime hard limit**. It must be finite (canonical "bounded" requirement).
- `configured_budget` **may differ from `recommended_max`**. The recommended range is guidance, not a safety ceiling — unless a future owner decision or safety analysis establishes one. This is an **unresolved owner decision**: whether `recommended_max` should ever be enforced as a hard limit.
- `configured_budget = 0` semantics are **an unresolved owner decision**. Canonical docs do not define whether 0 disables correction or is an invalid value. This must be decided separately.
- `configured_budget > recommended_max` is permissible **under the current model** — this is still "bounded" because it is finite and explicit. If a future decision makes `recommended_max` a hard limit, this constraint would change.
- **No hard upper bound is enforced by Agent Guard or this framework.** The finite nature of `configured_budget` satisfies the canonical "bounded" requirement.

**Evidence for this model:**
- `deferred-triggers.md`: "bounded REPLAN budget" — requires finiteness, not a specific value or enforcement mechanism.
- `backlog.md`: "correction loop bounds" — bounds as a concept required, not as a fixed constant.
- `agent-guard-capability-translation.md` §7: "A bounded retry budget permits it." — permits correction when a finite budget exists.

**No canonical source establishes that `recommended_max` must be a hard limit enforced by code.** Absent such evidence, `recommended_max` is guidance-only.

**Illustrative example (NOT a decision):**
```yaml
recommended_min: 1
recommended_max: 3
default: 2
configured_budget: <caller-provided finite integer>
```

**Blocked on:** Owner decision specifying all three numeric values before production implementation can proceed.

### When the budget is consumed (Owner Decision)

The budget is consumed when a correction round produces a new `AgentGuardResult` with `Decision == Deny`. Allow outcomes exit the loop immediately; RequireHumanApproval exits without consuming budget (see Decision C).

**Default assumption:** Each Deny-producing round consumes one budget unit, regardless of whether the next round succeeds or fails.

**Evidence:** No canonical source defines consumption semantics. This is an owner decision for Slice 33.

**Deferred:** Whether partial rounds (e.g., normalization failure mid-correction) consume budget.

### Whether correction after `Deny` / `RequireHumanApproval` counts as retry (Owner Decision)

- **After `Deny`: YES.** This is the canonical correction path per `agent-guard-capability-translation.md` §7 and `correction-loop.md`.
- **After `RequireHumanApproval`: NO.** `RequireHumanApproval` is a human-in-the-loop gate, not a replanning signal. Feeding it back to the model would bypass the human authorization step.

**Evidence:** `policy-outcome-replan-boundary.md` distinguishes `DENY` from `ASK_USER` / `RequireHumanApproval` at the system level. Budget consumption semantics for `RequireHumanApproval` are NOT defined in canonical docs — this is an owner decision for Slice 33.

**Default assumption:** `RequireHumanApproval` exits the loop without consuming budget, consistent with it being a separate control flow path.

### What happens on exhaustion (Owner Decision)

On budget exhaustion, the orchestration returns the last `AgentGuardResult` (a `Deny`) as the final outcome. No further correction attempts are made.

**Default assumption:** The structured event emitted records the final `Deny` — no special "exhausted" outcome type is introduced.

**Evidence:** No canonical source defines exhaustion behavior. This is an owner decision for Slice 33.

**Deferred:** The exact user-visible message on exhaustion is per `deferred-triggers.md` ("client-facing correction behavior").

---

## Decision C — Eligibility Matrix

| State | Eligible for Correction Loop | Rationale |
|-------|-----------------------------|-----------|
| `Allow` | **Not Eligible** | Action proceeds; no correction needed. Correction is secondary to denial. |
| `Deny` | **Eligible** | Canonical correction trigger per `agent-guard-capability-translation.md` §7 and `correction-loop.md`. |
| `RequireHumanApproval` | **Not Eligible** | Human-in-the-loop authorization; not a replanning signal. Correcting and resubmitting would bypass human review. |
| Structural validation failure (`NormalizationFailed`) | **Not Eligible** | Action could not be normalized to a semantic intent. The model needs a different raw action, not a correction loop around a failed normalization. (Deferred: may warrant a separate "regenerate" path, but not the correction loop.) |
| Translation failure (`TranslationFailed`) | **Not Eligible** | The normalized intent could not be mapped to a known capability. Same reasoning as above. |
| Policy evaluation failure (`PolicyEvaluationFailed`) | **Not Eligible** | Evaluation could not proceed; no decision was produced. Per `AgentGuardResult.PolicyEvaluationFailed`, this is a pipeline failure, not a denial. |
| Layer 3 finding (any `Layer3EvaluationResult`) | **Not Eligible** | Layer 3 produces an evaluation finding, not a `PolicyDecision`. The finding is informational; it does not deny the action. Per `layer-3-evaluation-result-boundary.md`, orchestration decides whether to escalate — not whether to correct. |
| Unavailable / degraded supervision | **Not Eligible** | Per `degraded-audit.md`: "Audit observation only — it does not produce a PolicyDecision, trigger correction, or invoke Layer 4." |
| Malformed / unusable correction input | **Not Eligible** | If orchestration cannot produce a valid correction request, the loop cannot start. The original result stands. |
| Retry budget exhausted | **Not Eligible** | By definition — the budget is consumed. The last result (Deny) stands. |

---

## Stop / Failure Semantics

### When the correction loop stops

The loop stops when any of the following occurs:
1. `AgentGuardResult.Decision == Allow` — action approved; proceed to execution.
2. `AgentGuardResult.Decision == RequireHumanApproval` — hand off to human; loop terminates.
3. Retry budget exhausted — return the last `Deny` result.
4. Non-DENY, non-Allow outcome from the pipeline (normalization failure, translation failure, policy evaluation failure) — return the failure result; loop terminates.

### Whether a failed correction attempt is a normal pipeline failure

A correction attempt that produces a `Deny` is a **normal DENY outcome** — it is not a pipeline failure. The orchestrator treats it the same as the first DENY: it consumes one budget unit and, if budget remains, offers another correction round.

A correction attempt that produces a `NormalizationFailed`, `TranslationFailed`, or `PolicyEvaluationFailed` is a **pipeline failure** for that round. The orchestrator returns that failure result immediately; the loop does not continue.

### Whether re-evaluation passes the full Agent Guard pipeline

**Yes (Default Assumption).** Each correction round re-enters the complete pipeline: normalization → translation → policy evaluation → (optional) Layer 3. No stage is skipped.

**Evidence:** `correction-loop.md`: "model → candidate → policy denial → ForgeGate corrective feedback → same model replans → policy evaluates again."

**Deferred:** Whether any stages can be shortcut (e.g., skip normalization if the correction only changes tool arguments). This is an implementation detail for Slice 33 stub; default is full re-entry.

### Whether each corrected action must re-evaluate policy (Owner Decision)

**Yes (Default Assumption).** Each corrected candidate is treated as a new `AgentAction` that enters the full Agent Guard pipeline from Stage 1. The pipeline does not cache or shortcut based on prior rounds.

**Evidence:** `correction-loop.md` flow implies full re-entry: "policy evaluates again" after "same model replans". However, this is an inference from the conceptual flow, not an explicitly stated canonical fact.

**Deferred:** Whether future optimization could skip stages for trivial corrections. Not addressed in Slice 33.

---

## Slice 33-B — Actionable Correction Feedback Contract

### 1. Canonical Evidence

| Source | Statement | Relevance |
|--------|-----------|-----------|
| `Docs/policy/correction-loop.md` | "model → candidate → policy denial → ForgeGate corrective feedback → same model replans" | Defines the flow: ForgeGate (orchestration) produces corrective feedback |
| `Docs/decisions/agent-guard-capability-translation.md` §7 | "The denial reason is actionable feedback the model can incorporate" | Requires denial reason in feedback; implies model generates correction itself |
| `Docs/decisions/policy-outcome-replan-boundary.md` | "Actionable feedback contract is deferred (no structured 'DenialReason' field exists in current `ProviderFailure`)" | Explicitly defers the feedback contract |
| `Docs/decisions/policy-outcome-replan-boundary.md` | "Correction loop is ORCHESTRATION concern, not policy concern" | Ownership: orchestration, not Agent Guard |
| `src/ForgeGate.Domain/AgentGuard/PolicyDecision.cs` | `PolicyDecision` has exactly 3 values: `Allow`, `Deny`, `RequireHumanApproval` | No new decision values permitted |
| `src/ForgeGate.Domain/AgentGuard/AgentGuardResult.cs` | `Reason` field carries the denial reason; `Capability`, `Target`, `Metadata`, `Layer3Result` carry evaluation context | Available data for feedback construction |

---

### 2. Correction Feedback Boundary

**Verdict: CANONICAL**

The boundary is:
```
Policy / Agent Guard → AgentGuardResult (Decision + Reason + context) → Orchestration → correction feedback → model
```

**Evidence:**
- `correction-loop.md`: "ForgeGate corrective feedback" — ForgeGate (orchestration layer) produces feedback, not Agent Guard.
- `policy-outcome-replan-boundary.md`: "Correction loop is ORCHESTRATION concern, not policy concern."
- `agent-guard-capability-translation.md` §7: "Model correction ... is a secondary mechanism, used only when ... the denial reason is actionable feedback the model can incorporate."

**Implication:** Agent Guard produces `AgentGuardResult` with `Decision`, `Reason`, `Capability`, `Target`, `Metadata`, `Layer3Result`. Orchestration consumes this result and constructs the correction feedback. No new PolicyDecision values, no new Agent Guard domain types.

---

### 3. Feedback Data Matrix

| Data Field | Source in AgentGuardResult | Classification | Rationale |
|------------|---------------------------|----------------|-----------|
| Original/raw action | `Observation.Source.RawAction` | **OWNER-DEFINED** | No canonical document explicitly requires raw action in feedback. Useful for context but orchestration decides what to include. |
| Normalized capability | `Capability` (`ActionIntentKind`) | **CANONICAL** | `agent-guard-capability-translation.md` §7: "the resulting capability was policy-denied" — capability is the denial subject. |
| Target | `Target` | **OWNER-DEFINED** | No canonical document explicitly requires target in feedback. Useful for replanning context but not canonically mandated. |
| Policy decision | `Decision` (`Deny`) | **CANONICAL** | Model must know the action was denied — implicit in "policy denial → ForgeGate corrective feedback". |
| Denial reason | `Reason` | **CANONICAL** | `agent-guard-capability-translation.md` §7: "The denial reason is actionable feedback the model can incorporate" — explicitly required. |
| Layer 3 finding | `Layer3Result` | **ALLOWED BUT OWNER DECISION** | Not canonically required for correction; orchestration may include it if useful. |
| Policy metadata | `Metadata` | **ALLOWED BUT OWNER DECISION** | May provide context; not canonically required. |
| Suggested correction | — | **NOT JUSTIFIED** | Principle that model generates correction itself is implied by §7 ("model can incorporate"), but explicit "no suggested fix" constraint is OWNER-DEFINED. |
| Arbitrary NL instructions | — | **NOT JUSTIFIED** | Would turn Policy into a planner; violates separation of concerns. |
| Internal implementation details | — | **NOT JUSTIFIED** | Violates boundary; not needed for replanning. |

---

### 4. Actionable Denial Semantics

**Verdict: DEFERRED — OWNER DECISION REQUIRED**

**Canonical basis:**
- `agent-guard-capability-translation.md` §7: "The denial reason is actionable feedback the model can incorporate"
- `policy-outcome-replan-boundary.md`: "Actionable feedback contract is deferred"

**What is canonical:**
- The denial reason (`PolicyEvaluationResult.Reason`) must be included in correction feedback.
- The model must be able to "incorporate" this feedback to produce a different candidate.

**What is NOT established:**
- Criteria for what makes a denial reason "actionable" vs. non-actionable.
- Whether some DENY outcomes produce actionable feedback while others do not.
- Whether the same denial reason is always actionable or depends on context.

**Owner decision required:** Define criteria for actionable denial. This requires empirical input from observing deterministic policy false positives and model replanning reliability.

---

### 5. Allowed Correction Scope

**Verdict: PARTIALLY CANONICAL, PARTIALLY UNRESOLVED**

**Canonical:**
- The model "replans" (`correction-loop.md`). This implies the model generates a new candidate action.

**Inferred from canonical (Default Assumption):**
- Tool arguments may change (e.g., different path, different flags).
- Tool selection may change (e.g., choose a different capability).
- Intent scope may change (narrower or differently scoped request).

**Unresolved owner decisions:**
- Whether the model may change the `ActionIntent` kind/target at the structural level — deferred per `backlog.md` ("ActionIntent schema").
- Whether the model may change authorization context (e.g., add approval requests) — NOT ESTABLISHED.
- Whether the model may change policy constraints — NOT ESTABLISHED; policy constraints are external to the model.

**Constraint:** Each corrected candidate re-enters the full Agent Guard pipeline from Stage 1. The policy layer evaluates the new candidate independently — no cached decisions, no bypass.

---

### 6. Contract Shape

**Verdict: DEFERRED**

**Canonical evidence:**
- `policy-outcome-replan-boundary.md`: "Actionable feedback contract is deferred (no structured 'DenialReason' field exists in current `ProviderFailure`)."

**Minimum conceptual fields (derived from `AgentGuardResult`):**
1. `rawAction` — the original denied action
2. `capability` — the normalized capability that was denied
3. `target` — the target/resource
4. `decision` — the policy decision (Deny)
5. `reason` — the denial reason

**No dedicated DTO is canonically required.** The orchestration layer may construct correction feedback from `AgentGuardResult` fields directly, or may define a dedicated DTO if operational needs require it. The exact shape is deferred.

---

### 7. Ownership Boundary

| Responsibility | Owner | Canonical Evidence |
|----------------|-------|-------------------|
| Producing denial facts | **Agent Guard / Policy Evaluator** | `PolicyEvaluationResult.Reason`; `AgentGuardResult` |
| Deciding correction eligibility | **Orchestration** | `policy-outcome-replan-boundary.md`: "DENY MAY become trigger for correction/replan flow; exact eligibility conditions are deferred" |
| Constructing feedback | **Orchestration** | `correction-loop.md`: "ForgeGate corrective feedback"; `policy-outcome-replan-boundary.md`: "Correction loop is ORCHESTRATION concern" |
| Invoking model again | **Orchestration** | `correction-loop.md`: "same model replans" |
| Tracking retry/replan budget | **Orchestration** | `policy-outcome-replan-boundary.md`: "Budget management: bounded retry" |
| Deciding when to stop | **Orchestration** | Based on `AgentGuardResult.Decision` and budget exhaustion |

---

### 8. Safety / Integrity Constraints

| Constraint | Status | Evidence |
|------------|--------|----------|
| No policy bypass | **CANONICAL** | `agent-guard-capability-translation.md` §7: correction is "secondary mechanism" |
| No conversion of `Deny` into `Allow` without re-evaluation | **CANONICAL** | Each round re-enters full pipeline (see §5) |
| No new `PolicyDecision` value | **CANONICAL** | `policy-outcome-replan-boundary.md`: 3-value enum fixed |
| No fabricated suggested fix | **OWNER-DEFINED** | Derived from principle that model generates correction (§7: "model can incorporate"), but no canonical document explicitly states "no suggested fix". The constraint follows from the architecture but is not canonically enumerated. |
| No mutation of authoritative policy state through correction feedback | **CANONICAL** | Each round is independent; policy is read-only from correction perspective |
| No unbounded loop | **CANONICAL** | "bounded retry budget" per `correction-loop.md`, `agent-guard-capability-translation.md` §7, `deferred-triggers.md` |

---

### 9. Unresolved Owner Decisions (Slice 33-B)

| Decision | Status | Required For |
|----------|--------|--------------|
| Actionable denial criteria | **TBD** | Empirical input (false-positive analysis, model reliability) |
| Whether Layer 3 finding may be included in feedback | **TBD** | Operational judgment |
| Whether metadata may be included in feedback | **TBD** | Operational judgment |
| Whether model may change `ActionIntent` kind/target structurally | **TBD** | `ActionIntent` schema decision |
| Whether model may change authorization context | **TBD** | Security analysis |
| Dedicated DTO vs. direct `AgentGuardResult` field mapping | **TBD** | Implementation preference |

---

### 10. Slice 33-B Verdict

**DECIDED FOR V0.1.**

The 33-B contract is fully owner-decided except for:
- D8 (actionable denial criteria) — deferred pending empirical input.
- D11 (concrete DTO representation) — deferred to implementation slice.
- `ActionIntent` kind structural changes — deferred per `backlog.md`.
- Authorization context changes — deferred pending security analysis.

**Slice 33-B is ready for owner decision review.** The framework boundaries are established; the remaining TBD items require empirical input before production implementation.

- **Recommended range** (`recommended_min`, `recommended_max`, `default`). Owner decision requiring empirical input.
- **Exact retry budget number** (e.g., max 1, 2, 3 rounds). Owner decision requiring empirical input.
- **Actionable feedback contract** — the precise DTO/structure of the correction request sent to the model.
- **ActionIntent schema** (kind/target/mechanism/scope fields and types) — necessary before correction can validate what the model is allowed to change.
- **Structural validation contract** — what is validated, where, how; prerequisite for knowing when a "malformed" correction is recoverable.
- **First deterministic allow/deny rules** — prerequisite for knowing which DENYs are actionable.
- **Timeout / circuit breaker conditions** — when to abort the loop beyond budget exhaustion.
- **Client-facing representation of correction failure** — what the user sees when the loop exhausts.
- **Layer 4 (semantic reasoner) interaction with correction** — whether a Layer 4 finding can trigger correction (deferred to Slice 36).

---

## Implementation Boundary

This decision enables Slice 33 implementation of:
- **Orchestration-layer correction loop ownership** — the loop is owned by the caller, not by Agent Guard.
- **Configurable budget parameter** — a stub accepting a tunable budget value (exact default TBD).
- **Eligibility gating** — correction invoked ONLY on `Deny` from a successfully evaluated pipeline.
- **Full pipeline re-entry** — default assumption; each round enters normalization → translation → policy evaluation → (optional) Layer 3.
- **No changes** to `PolicyDecision`, `AgentGuardResult`, `AgentGuardEvent`, or any Agent Guard domain type.

This decision does NOT enable:
- A concrete `ICorrectionLoop` interface signature — the actionable feedback DTO and model interaction contract remain deferred per `deferred-triggers.md` and `backlog.md`.
- Persistence for correction rounds (Slice 35).
- Admin UI for correction history (Slice 38).
- Semantic reasoner integration with correction (Slice 36).
- CompletionVerifier interaction with correction (Slice 37).

---

## Acceptance Criteria
Criteria below reflect only what is authorized by this decision. Items requiring deferred decisions are excluded.

- [ ] The correction loop framework is implemented as an **orchestration-layer concern** — no changes to `ForgeGate.Application.AgentGuard`.
- [ ] Eligibility gate prevents correction invocation for `Allow`, `RequireHumanApproval`, and all failure stages (`NormalizationFailed`, `TranslationFailed`, `PolicyEvaluationFailed`).
- [ ] Each correction round re-enters the full `AgentGuardService.Evaluate()` pipeline (default assumption; shortcutting deferred).
- [ ] No new `PolicyDecision` values introduced.
- [ ] No changes to existing `AgentGuardResult`, `AgentGuardEvent`, or `PolicyDecision` types.
- [ ] Budget is parameterized (configurable), not hardcoded. Default value is TBD by separate owner decision (see Deferred).

### Explicitly NOT covered by these criteria (deferred)
- Concrete `ICorrectionLoop` interface signature — awaiting actionable feedback contract.
- Numeric retry budget value — awaiting empirical owner decision.
- Client-facing exhaustion message — deferred per `deferred-triggers.md`.
- Timeout / circuit breaker — deferred.
- Structured event emission for correction rounds — depends on event schema extension (deferred).
- [ ] Structured event emitted for each correction round (using existing emitter; Slice 30 already in place).

---

## Slice 33-B — Owner Decision Worksheet

**Purpose:** Compact decision table for the remaining owner decisions in Slice 33-B (Actionable Correction Feedback Contract). Each row contains: Decision ID, Question, Canonical Evidence, Options, Implications, Proposed Wording (if supported), and Status.

---

### D1 — Decision field in feedback

| Field | Content |
|-------|---------|
| **Question** | Should `PolicyDecision` (`Deny`) be explicitly included in correction feedback? |
| **Canonical Evidence** | `Docs/policy/correction-loop.md`: "policy denial → ForgeGate corrective feedback". `Docs/decisions/policy-outcome-replan-boundary.md` §1: "DENY = policy evaluator says 'this candidate action cannot execute as-is'". |
| **Options** | (a) Include explicit `decision: Deny` field. (b) Omit — denial is implicit from presence of feedback. |
| **Implications** | (a) Explicit, unambiguous signal to model. (b) Saves tokens but may reduce clarity in edge cases. |
| **Decision** | **INCLUDE** — Correction feedback must explicitly communicate that the previous candidate was denied. For the correction-eligible path, the value is `Deny`. No new `PolicyDecision` value is introduced. |
| **Status** | **DECIDED FOR V0.1** |

---

### D2 — Reason field in feedback

| Field | Content |
|-------|---------|
| **Question** | Should the denial `Reason` be mandatory in correction feedback? |
| **Canonical Evidence** | `Docs/decisions/agent-guard-capability-translation.md` §7: "The denial reason is **actionable feedback** the model can incorporate." `Docs/decisions/policy-outcome-replan-boundary.md`: "REPLAN = orchestration control flow that MAY feed denial reason back to model for correction." |
| **Options** | (a) Mandatory. (b) Optional — include when available, omit when reason is empty/insufficient. |
| **Implications** | (a) Model always has the denial rationale. (b) Avoids passing empty or unhelpful reasons. |
| **Decision** | **INCLUDE — REQUIRED.** The denial reason is mandatory correction feedback. The reason must represent the actual policy/Agent Guard denial reason. Do not fabricate a reason. |
| **Status** | **DECIDED FOR V0.1** (also CANONICAL per §7) |

---

### D3 — Capability field in feedback

| Field | Content |
|-------|---------|
| **Question** | Should the normalized `Capability` (`ActionIntentKind`) be mandatory in correction feedback? Distinguish: Agent Guard result field vs. model-facing feedback content. |
| **Canonical Evidence** | `Docs/decisions/agent-guard-capability-translation.md` §7: "the resulting capability was policy-denied". `Docs/decisions/policy-outcome-replan-boundary.md`: DENY = "this candidate action cannot execute as-is" — the capability is the semantic identity of the denied action. |
| **Options** | (a) Mandatory. (b) Optional. (c) Omit — model only sees raw action. |
| **Implications** | (a) Model knows the semantic category denied. (b) Reduces token count. (c) Model must infer capability from raw action alone. |
| **Decision** | **INCLUDE — REQUIRED.** Include the normalized capability associated with the denied candidate. This gives orchestration/model-facing feedback a stable semantic representation of what capability was denied. Do not treat this as permission to mutate or bypass policy. |
| **Status** | **DECIDED FOR V0.1** |

---

### D4 — RawAction field in feedback

| Field | Content |
|-------|---------|
| **Question** | Should the original/raw action be included in correction feedback? |
| **Canonical Evidence** | `Docs/policy/correction-loop.md`: "same model replans" — the model generated the raw action and must receive feedback about it. No canonical document explicitly states whether the raw string must be echoed back. |
| **Options** | (a) Include always. (b) Include only when required for context (e.g., when capability/target are ambiguous). (c) Exclude — model already knows its original output. |
| **Implications** | (a) Maximum context for replanning. (b) Balanced approach. (c) Token-efficient but relies on model remembering its own output. |
| **Decision** | **INCLUDE — OPTIONAL.** `RawAction` may be included when useful for preserving candidate context. It is not mandatory for every correction feedback instance. Do not expose unrelated internal implementation details through this field. |
| **Status** | **DECIDED FOR V0.1** |

---

### D5 — Target field in feedback

| Field | Content |
|-------|---------|
| **Question** | Should `Target` be included in correction feedback? |
| **Canonical Evidence** | `AgentGuardResult.Target` exists as a field. No canonical document explicitly requires target in model-facing feedback. Target is part of the capability semantics but not separately mandated for feedback. |
| **Options** | (a) Include always. (b) Include when capability semantics require it. (c) Exclude — target is implicit in raw action. |
| **Implications** | (a) Full context. (b) Context-aware inclusion. (c) Minimal feedback. |
| **Decision** | **INCLUDE — OPTIONAL.** `Target` may be included as contextual information for replanning. It is not mandatory for every correction feedback instance. |
| **Status** | **DECIDED FOR V0.1** |

---

### D6 — Layer 3 finding in feedback

| Field | Content |
|-------|---------|
| **Question** | Should `Layer3Result` be passed into correction feedback? Consider whether Layer 3 is: (a) policy-denial context, (b) a separate evaluation layer, (c) potentially sensitive/internal evidence. |
| **Canonical Evidence** | `Docs/decisions/layer-3-evaluation-result-boundary.md`: "Layer 3 communicates **what its risk/suspicion evaluation found**... Orchestration decides **what to do with that finding**." "Layer 3 does NOT own... PolicyDecision production, REPLAN production." Pipeline position: Layer 3 → Orchestration → Layer 4 → Policy Decision → REPLAN. Layer 3 is a **separate evaluation layer**, not policy-denial context. |
| **Options** | (a) Include — risk/suspicion finding may inform replanning. (b) Exclude — Layer 3 is internal evaluation; denial reason suffices. (c) Include only when `HasRiskFinding == true`. |
| **Implications** | (a) Model sees risk signal alongside denial. (b) Cleaner separation of concerns. (c) Targeted inclusion reduces noise. |
| **Decision** | **EXCLUDE FROM THE BASE CORRECTION CONTRACT.** Do not make `Layer3Result` part of the base correction feedback contract. Layer 3 is a separate evaluation layer and the existing correction contract should remain focused on actionable policy-denial feedback. If a future owner decision establishes a specific Layer 3 → correction interaction, that should be handled separately. Do not implement that future interaction now. |
| **Status** | **DECIDED FOR V0.1** |

---

### D7 — Metadata field in feedback

| Field | Content |
|-------|---------|
| **Question** | Should generic `AgentGuardResult.Metadata` cross the correction feedback boundary? |
| **Canonical Evidence** | `AgentGuardResult.Metadata` is an optional `string?` field with no defined contract in canonical docs. It is described as carrying evaluation context but has no schema, no semantics, and no bounded usage documented. |
| **Options** | (a) Include — may carry useful context. (b) Exclude — unbounded metadata bag risks leaking internal state. (c) Include only specific, documented metadata keys. |
| **Implications** | (a) Maximum flexibility, maximum risk. (b) Safe but may lose useful context. (c) Balanced but requires metadata schema definition. |
| **Decision** | **EXCLUDE FROM THE BASE CORRECTION CONTRACT.** Do not expose the generic `AgentGuardResult.Metadata` bag through the correction feedback contract. The metadata field has no sufficiently bounded model-facing contract and should not become an uncontrolled extension point. Specific metadata can be added later only through an explicit owner decision. |
| **Status** | **DECIDED FOR V0.1** |

---

### D8 — Actionable denial criteria

| Field | Content |
|-------|---------|
| **Question** | What qualifies a denial as "actionable feedback" sufficient to permit correction? |
| **Canonical Evidence** | `Docs/decisions/agent-guard-capability-translation.md` §7: "The denial reason is **actionable feedback** the model can incorporate." `Docs/decisions/policy-outcome-replan-boundary.md`: "Actionable feedback contract is deferred (no structured 'DenialReason' field exists in current `ProviderFailure`)." |
| **Options** | (a) All DENY outcomes are actionable by default. (b) Only DENY outcomes with a non-empty, semantically meaningful reason are actionable. (c) Define a taxonomy of actionable vs. non-actionable denials (e.g., structural deny = actionable, policy override = not actionable). |
| **Implications** | (a) Simplest implementation, may waste correction budget on non-actionable denials. (b) Requires reason quality check. (c) Most precise but requires empirical taxonomy definition. |
| **Decision** | **KEEP DEFERRED.** Do not invent a taxonomy or numeric/actionability heuristic now. Canonical architecture requires correction only when the denial reason is actionable, but the repository does not yet establish a complete actionable-denial classification. Keep this as a deferred/empirical decision. Do not block recording the rest of the 33-B contract on inventing this taxonomy. |
| **Status** | **DEFERRED — EMPIRICAL INPUT REQUIRED** |

---

### D9 — Correction scope (what the model may change)

| Field | Content |
|-------|---------|
| **Question** | For a corrected candidate, what may the model change? |
| **Canonical Evidence** | `Docs/policy/correction-loop.md`: "same model replans" — model generates a new candidate. `Docs/decisions/policy-outcome-replan-boundary.md`: "DENY = this candidate action cannot execute as-is" — the *candidate* changes, not the policy. |
| **Items** | |
| Tool arguments | **PERMITTED** — model replanning implies argument adjustment is allowed. |
| Tool selection | **PERMITTED** — "choose another compliant path" (`outcomes.md`). |
| Intent scope | **PERMITTED** — narrowing scope is consistent with replanning. |
| `ActionIntent` kind | **DEFERRED** — `backlog.md`: "ActionIntent schema (kind/target/mechanism/scope fields and types)" is explicitly deferred. |
| Target | **PERMITTED** — changing target is consistent with replanning. |
| Authorization context | **OWNER DECISION** — no canonical guidance; security-sensitive. |
| Policy constraints | **NOT PERMITTED** — policy constraints are external to the model; the model cannot change policy, only its action to comply with it. |
| **Decision** | Record the following:<br><br>**Allowed by the correction-loop contract:** The model may produce a new candidate that changes: tool arguments; tool selection; intent scope — provided the resulting candidate is independently evaluated again.<br><br>**Not authorized by this decision:** Do NOT establish new semantics for changing `ActionIntent` kind structurally; changing authorization context; changing policy constraints. Keep the unresolved/deferred `ActionIntent` schema questions as documented. Do not interpret correction feedback as authorization to alter security or policy state. |
| **Status** | Mixed — see detailed analysis above. |

---

### D10 — Pipeline re-entry

| Field | Content |
|-------|---------|
| **Question** | Should every corrected candidate re-enter the complete Agent Guard pipeline? |
| **Canonical Evidence** | `Docs/policy/correction-loop.md`: "model → candidate → policy denial → ForgeGate corrective feedback → same model replans → **policy evaluates again**." `Docs/decisions/policy-outcome-replan-boundary.md`: "REPLAN = orchestration control flow that MAY feed denial reason back to model for correction, bounded by retry budget." |
| **Distinction** | `new candidate → policy evaluates again` = **CANONICAL** (explicitly stated: "policy evaluates again").<br>`full pipeline re-entry through every Agent Guard stage (normalize → translate → evaluate → Layer 3)` = **OWNER-DEFINED DEFAULT CONTRACT** (reasonable default, but canonical docs do not enumerate each stage). |
| **Options** | (a) Full pipeline re-entry (default safe contract). (b) Skip normalization (corrected action is already normalized). (c) Skip translation (capability is unchanged). |
| **Implications** | (a) Safest — each round is independently evaluated through all stages. (b) Optimization — assumes correction only changes arguments. (c) Risk — if correction changes capability, translation must re-run. |
| **Decision** | **FULL PIPELINE RE-ENTRY.** Every corrected candidate must go through the normal Agent Guard evaluation pipeline again.<br><br>Conceptually:<br>```\ndenied candidate\n    ↓\ncorrection feedback\n    ↓\nsame model replans\n    ↓\nnew candidate\n    ↓\nfull Agent Guard pipeline\n    ↓\nnew independent policy evaluation\n```\nThe canonical documentation explicitly establishes that policy evaluates the new candidate again. The owner decision now additionally establishes that correction must not shortcut the normal pipeline. Do not treat this as permission to implement pipeline changes in this task. |
| **Status** | **DECIDED FOR V0.1** |

---

### D11 — Contract representation

| Field | Content |
|-------|---------|
| **Question** | Does 33-B require a dedicated structured DTO/contract for correction feedback? Distinguish from the separately deferred question of what the feedback contract *contains*. |
| **Canonical Evidence** | `Docs/decisions/policy-outcome-replan-boundary.md`: "Actionable feedback contract is deferred (no structured 'DenialReason' field exists in current `ProviderFailure`)." — This defers the **content** of the feedback contract, not the DTO representation. |
| **Separate question** | Whether implementation should use a dedicated DTO, direct mapping from `AgentGuardResult`, or another representation is an **implementation/design owner decision** — not covered by the canonical deferral. |
| **Options** | (a) Dedicated DTO (`CorrectionFeedback` or similar). (b) Orchestration maps directly from `AgentGuardResult` fields. (c) Defer representation until implementation — use conceptual contract now. |
| **Implications** | (a) Explicit boundary, requires new type. (b) No new types, relies on existing fields. (c) Flexibility, no commitment. |
| **Decision** | **DEFER IMPLEMENTATION REPRESENTATION.** Do not choose between dedicated DTO, direct mapping, or another concrete implementation representation. That is an implementation detail for the later implementation slice. The conceptual contract is decided; the concrete C# representation remains deferred. |
| **Status** | **DEFERRED** |

---

### D12 — Integrity constraints

| Field | Content |
|-------|---------|
| **Question** | Separate canonical constraints from owner contract candidates. |
| **Already Canonical** | |
| No policy bypass | `agent-guard-capability-translation.md` §7: correction is "secondary mechanism" |
| Corrected candidate must be re-evaluated | `correction-loop.md`: "policy evaluates again" |
| No new `PolicyDecision` value | `policy-outcome-replan-boundary.md`: 3-value enum fixed |
| Bounded correction loop | `correction-loop.md`, `agent-guard-capability-translation.md` §7, `deferred-triggers.md`: "bounded retry budget" |
| **Owner-Confirmed Contract Constraints** (not previously canonically enumerated) | |
| A correction must never bypass policy evaluation | Owner-confirmed safeguard |
| A corrected candidate must be independently re-evaluated | Owner-confirmed safeguard |
| Correction does not introduce a new `PolicyDecision` | Owner-confirmed safeguard |
| Correction feedback must not mutate authoritative policy state | Owner-confirmed safeguard |
| The correction loop remains bounded by the configured finite retry/replan budget from Slice 33-A | Owner-confirmed safeguard |
| The model, not Policy, generates the corrected candidate | Owner-confirmed safeguard |
| ForgeGate must provide denial facts, not fabricate an alternative authorized action | Owner-confirmed safeguard |
| **Status** | See table above. (4 canonical + 7 owner-confirmed = 11 total constraints) |

---

## Summary Tables

### 1. Owner Decision Matrix (D1–D12)

| ID | Decision | Status |
|----|----------|--------|
| D1 | Decision field in feedback | **DECIDED FOR V0.1** — Include (`Deny`) |
| D2 | Reason field mandatory | **DECIDED FOR V0.1** — Required |
| D3 | Capability field mandatory | **DECIDED FOR V0.1** — Required |
| D4 | RawAction field | **DECIDED FOR V0.1** — Optional |
| D5 | Target field | **DECIDED FOR V0.1** — Optional |
| D6 | Layer 3 finding in feedback | **DECIDED FOR V0.1** — Excluded from base contract |
| D7 | Metadata in feedback | **DECIDED FOR V0.1** — Excluded from base contract |
| D8 | Actionable denial criteria | **DEFERRED** — Empirical input required |
| D9 | Correction scope | **DECIDED FOR V0.1** — Per detailed analysis (tool args/selection/scope allowed; ActionIntent kind/authorization/policy constraints deferred) |
| D10 | Pipeline re-entry | **DECIDED FOR V0.1** — Full pipeline re-entry |
| D11 | Contract representation | **DEFERRED** — Implementation detail |
| D12 | Integrity constraints | **DECIDED FOR V0.1** — 4 canonical + 7 owner-confirmed |

### 2. Canonical Constraints (explicitly cited)

1. `correction-loop.md`: "policy denial → ForgeGate corrective feedback → same model replans → policy evaluates again"
2. `agent-guard-capability-translation.md` §7: "The denial reason is actionable feedback the model can incorporate"
3. `agent-guard-capability-translation.md` §7: correction is "secondary mechanism"
4. `policy-outcome-replan-boundary.md`: `PolicyDecision` has exactly 3 values
5. `policy-outcome-replan-boundary.md`: "Correction loop is ORCHESTRATION concern, not policy concern"
6. `deferred-triggers.md`: "bounded REPLAN budget"
7. `layer-3-evaluation-result-boundary.md`: Layer 3 is a separate evaluation layer; orchestration decides escalation
8. `policy-outcome-replan-boundary.md`: "Actionable feedback contract is deferred"

### 3. Owner Decisions Required (genuine)

| Decision | Required For |
|----------|-------------|
| D8: Actionable denial criteria | Empirical input (false-positive analysis) |
| D11: DTO vs. direct mapping | Implementation preference (separate from content deferral) |

### 4. Deferred Decisions

| Decision | Deferred To | Reason |
|----------|-------------|--------|
| D8: Actionable denial criteria | Post-empirical review | Requires false-positive rate data |
| D11: Contract representation | `policy-outcome-replan-boundary.md` | "Actionable feedback contract is deferred" (content, not DTO) |
| D9: `ActionIntent` kind changes | `backlog.md` | "ActionIntent schema" deferred |
| D9: Authorization context | Future security analysis | Not established in current scope |

### 5. Owner-Decided Contract — Slice 33-B (V0.1)

> **OWNER-DECIDED CONTRACT — SLICE 33-B (V0.1)**
>
> **Required fields:** `decision` (Deny), `reason`, `capability`.
> **Optional fields:** `rawAction`, `target` (included when useful for context).
> **Excluded from base contract:** `layer3Result`, `generic metadata`, `suggestedFix`, `arbitrary policy instructions`, `internal implementation details`.
> **Contract representation:** Deferred — concrete DTO/implementation is an implementation detail for a later slice.
> **Pipeline:** Full Agent Guard pipeline re-entry for every corrected candidate. No stage shortcutting.
> **Ownership:** Orchestration constructs feedback from `AgentGuardResult`; Agent Guard does not produce correction feedback.
> **Integrity:** No policy bypass, no new `PolicyDecision` values, bounded loop (per Slice 33-A), independent re-evaluation per round, model generates correction (not Policy), ForgeGate provides denial facts (not fabricated alternatives).
> **Scope:** Model may change tool arguments, tool selection, intent scope. `ActionIntent` kind structural changes, authorization context changes, and policy constraint changes remain deferred per `backlog.md` and this decision.
> **Deferred:** D8 (actionable denial criteria), D11 (concrete DTO representation), `ActionIntent` schema (per `backlog.md`)

**DECIDED FOR V0.1.**

The 33-B contract is fully owner-decided except for:
- D8 (actionable denial criteria) — deferred pending empirical input.
- D11 (concrete DTO representation) — deferred to implementation slice.
- `ActionIntent` kind structural changes — deferred per `backlog.md`.
- Authorization context changes — deferred pending security analysis.

The conceptual contract is established and ready for implementation in a future slice (pending Slice 33-A numeric budget decision).
