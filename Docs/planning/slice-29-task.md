# Slice 29: Layer 3 Risk/Suspicion Evaluation Integration

**Status:** PROPOSED
**Parent Milestone:** milestone-gateway-agent-guard
**Priority:** HIGH
**Created:** 2026-09-21
**Slice:** 29 (following Slice 28 completion)

---

## Goal

Integrate Layer 3 risk/suspicion evaluation into the Agent Guard pipeline as a bounded gate between deterministic policy and optional semantic reasoner, establishing the minimal semantic contract for cross-session protection without implementing full semantic escalation.

---

## Scope (In Scope)

### Implementation
1. **Layer 3 evaluation seam** — Create minimal abstraction for risk/suspicion evaluation that:
   - Accepts contextual evidence from orchestration (Option B)
   - Produces an evaluation finding with the closed semantic distinction: present/absent risk/suspicion
   - Does NOT produce PolicyDecision, REPLAN, or escalation commands
   - Does NOT invoke Layer 4 directly

2. **Pipeline integration** — Wire Layer 3 into existing Agent Guard flow:
   - Position: after deterministic policy, before potential Layer 4 invocation
   - Orchestration receives result and decides whether to invoke semantic reasoner
   - Existing PolicyDecision values remain unchanged (Allow/Deny/RequireHumanApproval)

3. **Cross-session evidence integration (MVP)** — Establish Layer 3 evaluation seam so contextual evidence can be consumed:
   - Accept contextual evidence supplied by orchestration (Option B)
   - Evaluate whether candidate action has risk/suspicion indicators
   - Produce evaluation finding with present/absent distinction
   - Does NOT implement cross-session attribution infrastructure
   - Does NOT define how foreign attribution maps to risk finding
   - Does NOT resolve session identity or lifecycle

4. **Tests** — Verify:
   - Layer 3 evaluation occurs at correct pipeline position
   - Evaluation finding semantically distinguishes present/absent risk
   - Orchestration interprets result without Layer 3 deciding escalation
   - Existing PolicyDecision behavior unchanged
   - Non-tool-call flow unchanged
   - Deterministic Agent Guard behavior unchanged

---

## Non-Goals (Explicitly Deferred)

The following are OUT OF SCOPE for Slice 29:

- **Layer 4 semantic reasoner** — Invocation logic deferred
- **Semantic escalation policy** — Threshold/criteria deferred (but bounded semantic escalation IS MVP scope per `mvp-scope.md`)
- **Risk score representation** — Deferred (no numeric scores)
- **Risk taxonomy** — Deferred (no Low/Medium/High, Clear/Suspicious enums)
- **Confidence model** — Deferred
- **Reason/actionable feedback** — Deferred
- **Policy mapping** — Deferred (how evaluation finding maps to Allow/Deny)
- **REPLAN mechanics** — Deferred (orchestration control flow)
- **Correction loop** — Deferred
- **SessionResolver API** — Deferred
- **Contextual infrastructure** — Option B is DECIDED: caller/orchestration supplies contextual evidence; Layer 3 does not resolve contextual infrastructure. Concrete infrastructure/API semantics remain deferred.
- **Attribution state taxonomy** — Deferred (NoRelevantAttribution/ForeignAttribution/Unavailable are PROPOSED, not canonical)
- **Unavailable/failure semantics** — Deferred
- **Admin UI** — Deferred
- **Persistence/Activity logging** — Deferred

---

## Architectural Constraints

Must comply with existing canonical decisions:

1. **Context Ownership = Option B** (`Docs/decisions/agent-guard-capability-translation.md`)
   - Orchestration supplies contextual evidence
   - Layer 3 does not resolve session identity

2. **Layer 3 gate-only boundary** (`Docs/decisions/layer-3-evaluation-result-boundary.md`)
   - Layer 3 evaluates, does not enforce
   - No PolicyDecision production
   - No direct Layer 4 invocation

3. **REPLAN boundary** (`Docs/decisions/policy-outcome-replan-boundary.md`)
   - PolicyDecision remains Allow/Deny/RequireHumanApproval
   - REPLAN is orchestration control flow

4. **MVP scope** (`Docs/policy/mvp-scope.md`)
   - Basic cross-session protection in scope
   - Bounded semantic escalation IS MVP scope (not deferred); exact criteria/thresholds deferred

---

## Closed Decisions Referenced

| Decision | Source | Status |
|----------|--------|--------|
| Context Ownership = Option B | `agent-guard-capability-translation.md` | LOCKED |
| ForeignSessionId not required | `layer-3-attribution-contract.md` | DECIDED |
| CurrentSessionAttribution not distinct state | `layer-3-attribution-contract.md` | DECIDED |
| Layer 3 produces evaluation finding | `layer-3-evaluation-result-boundary.md` | DECIDED |
| Layer 3 finding MUST distinguish present/absent | `layer-3-evaluation-result-boundary.md` | DECIDED |
| REPLAN ≠ PolicyDecision | `policy-outcome-replan-boundary.md` | DECIDED |
| PolicyDecision = Allow/Deny/RequireHumanApproval | `policy-outcome-replan-boundary.md` | DECIDED |

---

## Deferred Items (Preserved from ADRs)

The following remain OPEN and are NOT addressed by Slice 29:

- Exact semantic contents of evaluation finding (beyond present/absent)
- Concrete result representation (DTO/class/enum/boolean)
- Risk/suspicion taxonomy
- Risk score representation
- Escalation threshold values
- Confidence representation
- Reason/actionable feedback structure
- Attribution state taxonomy
- Unavailable/failure semantics
- Exact Layer 3 API contract
- Layer 4 contract
- Policy mapping
- Correction-loop semantics

---

## Acceptance Criteria

1. Layer 3 evaluation occurs at intended pipeline position (after deterministic policy, before potential Layer 4)
2. Layer 3 produces evaluation finding that semantically distinguishes present/absent risk/suspicion
3. Orchestration receives and interprets result without Layer 3 directly deciding escalation
4. Existing PolicyDecision values remain unchanged (Allow/Deny/RequireHumanApproval)
5. Non-tool-call flow remains unchanged
6. Deterministic Agent Guard behavior remains unchanged
7. Existing tests continue to pass
8. New tests cover Layer 3 seam and both semantic states (present/absent) where testable
9. No Layer 4 invocation logic implemented
10. No score/threshold/confidence/reason fields defined
11. No specific bool/enum/DTO name enforced (representation is open)
12. No cross-session attribution infrastructure implemented (only seam established)

---

## Evidence Sources

- `Docs/decisions/layer-3-attribution-contract.md`
- `Docs/decisions/layer-3-evaluation-result-boundary.md`
- `Docs/decisions/policy-outcome-replan-boundary.md`
- `Docs/decisions/agent-guard-capability-translation.md`
- `Docs/supervision/action-policy.md`
- `Docs/policy/tool-enforcement.md`
- `Docs/policy/semantic-escalation.md`
- `Docs/policy/mvp-scope.md`
- `Docs/supervision/implementation-order.md` (Step 10 = Semantic policy escalation)

---

## Implementation Notes

### Semantic Distinction
The closed semantic requirement is:
> L3 finding communicates whether relevant risk/suspicion was present or absent.

**Representation is OPEN.** The implementation must realize this semantic distinction, but the concrete mechanism (boolean, enum, DTO, etc.) is not prescribed by the architectural decisions. The implementation agent should choose the smallest representation compatible with existing architecture, without establishing it as canonical long-term contract.

### Pipeline Position

```
Candidate Action
    ↓
Structural Validation (Layer 1)
    ↓
Deterministic Policy (Layer 2) → PolicyDecision
    ↓
Layer 3: Risk/Suspicion Evaluation → Evaluation Finding (present/absent)
    ↓
Orchestration: Interprets finding, decides escalation
    ↓
Layer 4: Semantic Reasoner (optional, if orchestrated)
    ↓
Final PolicyDecision (Allow/Deny/RequireHumanApproval)
```

---

## Certification Plan

```bash
# Focused tests
dotnet test ForgeGate.sln -c Release --filter "FullyQualifiedName~Layer3"
dotnet test ForgeGate.sln -c Release --filter "FullyQualifiedName~AgentGuard"

# Full suite
dotnet test ForgeGate.sln -c Release --no-build
# Expected: All existing tests pass + new Layer 3 tests
```
