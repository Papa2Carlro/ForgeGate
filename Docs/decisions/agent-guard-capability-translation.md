# Agent Guard: Capability Translation Architecture Decision

**Status:** Locked  
**Date:** 2025-01-15  
**Slice:** 17.1  
**Refs:** `Docs/architecture/product/gateway-and-agent-guard.md`, `Docs/policy/policy-enforcement.md`

---

## Decision

Agent Guard's primary safety mechanism shall be **Capability Translation / Intent Normalization**, not model-level deny/correction loops.

---

## Core Principles

### 1. Capability Translation is Primary Mode

Agent Guard translates raw agent actions into semantic capabilities before any enforcement decision. The translation pipeline:

```
Agent Action (raw) → Intent/Capability Normalization → Translation/Execution Strategy
```

**Canonical example:**
```
run_terminal("cat /workspace/foo.txt")
  → FileRead("/workspace/foo.txt")
  → native execution (if policy allows)
```

Translation extracts the semantic intent from syntactic surface form and maps it to a known capability. Only the normalized capability enters policy evaluation.

### 2. Model is NOT the Primary Safety Mechanism

Do NOT rely on the LLM itself to police its own output. Reject architectures that:
- Return corrections iteratively until the model "gets it right"
- Use the model as a gatekeeper for its own actions
- Allow unlimited correction rounds

The model generates candidates; Agent Guard enforces boundaries. Enforcement is external, deterministic, and bounded.

### 3. Policy Boundary is Semantic Capability, Not Raw Command

Policy operates on normalized `ActionIntent` (kind, target, mechanism, scope), not raw command strings. This is consistent with existing policy enforcement architecture (`Docs/policy/policy-enforcement.md`).

A rule like "deny `rm -rf /`" is fragile. A rule like "deny `FileDelete` on paths matching `/` root scope" is robust — it catches the intent regardless of syntactic disguise.

### 4. Translation ≠ Unconditional Allow

Translation normalizes intent; it does not bypass policy. After translation:
1. The normalized `ActionIntent` flows through the existing deterministic policy layer
2. If policy denies, the action is rejected with structured reasoning
3. If policy allows, the action proceeds via the translated execution strategy

Translation and policy are separate stages. One does not imply the other.

### 5. Native Execution is Preferred When Safe

When intent is reliably normalized AND policy permits, execute natively — not through the model, not through arbitrary shell interpretation.

Native execution means: call the appropriate infrastructure method directly (file read, process spawn, HTTP request) with the translated parameters. This eliminates the model as an execution intermediary and reduces attack surface.

### 6. Unknown/Complex Actions Remain Policy-Controlled

If an action cannot be translated to a known capability:
- It does NOT get a default "allow" or "auto-translate" fallback
- It remains under policy control
- Policy evaluates it against deterministic rules (deny-by-default for unknown capabilities)
- No invented fallback mechanism shortcuts this

The system is safe by omission: unrecognized capabilities are denied, not guessingly translated.

### 7. Retraining/Correction is Secondary Mode

Model correction (bounded replan loops per `Docs/policy/correction-loop.md`) is a secondary mechanism, used only when:
- The translation succeeded but the resulting capability was policy-denied
- The denial reason is actionable feedback the model can incorporate
- A bounded retry budget permits it

Correction is NOT the primary path. It is a recovery mechanism for otherwise-legitimate intents that were mistakenly expressed in disallowed form.

### 8. Structured Runtime/System Event is Source of Truth

Every Agent Guard intervention produces a structured event:
- Translation decision (raw → normalized)
- Policy evaluation result (allow/deny/review)
- Execution outcome (success/failure/rejected)
- Correction round (if applicable)

These events are append-only, auditable, and queryable. They are the canonical record — not logs, not UI state, not model memory.

### 9. System Alert is Presentation of Structured Event

User-facing alerts (System Alert) are derived from structured events, not independent UI constructs. The alert layer presents:
```
StructuredEvent → AlertFormatter → User-visible notification
```

This ensures alerts are deterministic, reproducible, and traceable to the underlying guard decision. No standalone alert logic exists outside the event pipeline.

---

## Architecture Boundaries

This decision respects the existing boundary:
> "Gateway must NOT depend on Agent Guard. Agent Guard (future, optional): tool execution, capability enforcement, approvals, stronger guarantees. Leave clean boundaries; do not implement Agent Guard runtime now."

— `Docs/architecture/product/gateway-and-agent-guard.md`

Implementation note: The capability translation layer and policy integration points shall be designed as injectable interfaces, so the gateway remains independent until Agent Guard is ready for runtime integration.

---

## Implications

| Aspect | Impact |
|--------|--------|
| Policy engine | Extends to handle `ActionIntent` schema (kind/target/mechanism/scope) |
| Providers | No change — providers remain unaware of Agent Guard |
| Router | No change — routing is orthogonal to capability enforcement |
| Streaming | No change — streaming commit boundaries are independent |
| Supervision | Extends `execution-supervision.md` detectors to operate on normalized capabilities |
| Admin UI | Future: can query structured events for audit/monitoring |

---

## Open Questions (Deferred)

- Exact `ActionIntent` schema (kind/target/mechanism/scope fields and types)
- Capability registry: how capabilities are declared, versioned, discovered
- Translation engine: rule-based vs. learned vs. hybrid
- Correction loop bounds: max rounds, timeout, circuit breaker

These are deferred to Slice 18+ per `Docs/supervision/deferred-triggers.md`: "BEFORE advanced Agent Guard: hard execution-boundary integrations."

---

## Acceptance Criteria

- [x] Capability Translation is documented as primary mode
- [x] Model correction/retraining is secondary, bounded
- [x] Semantic capability is the policy boundary
- [x] Translation ≠ automatic allow (policy still applies)
- [x] Native execution preferred when intent normalized + policy allows
- [x] Unknown/complex actions stay policy-controlled (no invented fallback)
- [x] Structured runtime event is source of truth
- [x] System Alert is presentation of structured event
- [ ] No production code changes ( Slice 17.1 is documentation-only )
- [ ] No test changes
- [ ] No commit
