# Auditability of Degraded Supervision

**Status:** DECIDED (Slice 32 owner decision)
**Date:** 2026-09-22
**Refs:** `Docs/decisions/agent-guard-capability-translation.md` §8, `Docs/planning/milestone-gateway-agent-guard.md` (Slice 32)

---

## Decision

When ForgeGate allows/rejects an action because a supervision subsystem is degraded or unavailable, record an explicit structured audit event using the existing `AgentGuardEvent` infrastructure. This event is an **audit observation only** — it does not produce a PolicyDecision, trigger correction, or invoke Layer 4.

### Event Model

Use the existing `AgentGuardEvent` type with a new `AgentGuardOutcomeType` value. Do NOT create a separate `DegradedAuditEvent` type.

**Rationale:** The existing event pipeline (Slice 30) already supports arbitrary outcome types via the `OutcomeType` enum. Creating a parallel event hierarchy would duplicate the emitter, correlation, and timestamp infrastructure unnecessarily.

### Event Naming

Canonical event name: `DegradedSupervision`

Add to `AgentGuardOutcomeType`:
```csharp
DegradedSupervision
```

### Schema

| Field | Type | Required | Source |
|-------|------|----------|--------|
| `EventId` | `Guid` | Yes | Inherited from `AgentGuardEvent` |
| `CreatedAt` | `DateTime` | Yes | Inherited |
| `Observation.CorrelationId` | `Guid` | Yes | Inherited from `AgentActionObservation` |
| `OutcomeType` | `AgentGuardOutcomeType` | Yes | Set to `DegradedSupervision` |
| `Decision` | `PolicyDecision?` | Yes | The policy decision made despite degradation |
| `Capability` | `ActionIntentKind?` | Yes | The capability being evaluated |
| `Target` | `string?` | Yes | The action target |
| `DegradedCondition` | `string` | Yes | One of: `SemanticPolicyUnavailable`, `CompletionVerifierUnavailable`, `AllowedUnderFailOpen`, `DeniedUnderFailClosed`, `UserEscalationRequired` |
| `DegradationReason` | `string?` | No | Optional context about why supervision was unavailable |
| `Layer3Result` | `Layer3EvaluationResult?` | No | If Layer 3 was also unavailable, capture its null state |

**New field:** `DegradedCondition` — distinguishes the type of degradation. Uses string to avoid premature enum commitment; can be refined to enum in future slice.

### MVP Scope (Slice 32)

Implement recording for these 3 conditions (derived from existing fail-open/fail-closed docs):

1. **`AllowedUnderFailOpen`** — Low-risk action allowed because semantic supervisor unavailable (per `fail-open.md`)
2. **`DeniedUnderFailClosed`** — High-risk action denied because semantic supervisor unavailable (per `fail-closed.md`)
3. **`SemanticPolicyUnavailable`** — Generic semantic policy subsystem unavailable

**Deferred** (out of scope for Slice 32):
- `CompletionVerifierUnavailable` — requires Slice 37 (CompletionVerifier)
- `UserEscalationRequired` — requires orchestration context not yet modeled

### Trigger Point

**NOT YET IMPLEMENTED** — deferred to Slice 32 implementation.

When degradation is detected in the orchestration layer, call:
```csharp
_emitter.Emit(AgentGuardEvent.FromDegradedSupervision(
    result: guardResult,
    observation: observation,
    degradedCondition: "AllowedUnderFailOpen",
    degradationReason: "Semantic supervisor timeout"
));
```

Factory method `FromDegradedSupervision` to be added to `AgentGuardEvent` in Slice 32.

### Boundaries

- **Audit event ≠ PolicyDecision** — Decision field captures the resulting policy decision; event records that it was made under degraded conditions
- **Audit event ≠ System Alert** — Alert formatting (Slice 31) may choose to include this event; audit is source of truth
- **Audit event ≠ Correction command** — No retry, replan, or correction triggered by audit event
- **Audit event ≠ Layer 4 invocation** — No semantic reasoner called by audit event

### Out of Scope for Slice 32

- Persistence / database (deferred to Slice 35)
- Query/replay API (deferred to Slice 38)
- Alert delivery/injection (deferred to Slice 31 consumer)
- Correction loop / retries (deferred to Slice 33)
- CompletionVerifier integration (deferred to Slice 37)
- Admin UI (deferred to Slice 38)

---

## Examples

### AllowedUnderFailOpen
```csharp
new AgentGuardEvent
{
    OutcomeType = AgentGuardOutcomeType.DegradedSupervision,
    Decision = PolicyDecision.Allow,
    Capability = ActionIntentKind.FileRead,
    Target = "/workspace/foo.txt",
    DegradedCondition = "AllowedUnderFailOpen",
    DegradationReason = "Semantic supervisor timeout after 5s"
}
```

### DeniedUnderFailClosed
```csharp
new AgentGuardEvent
{
    OutcomeType = AgentGuardOutcomeType.DegradedSupervision,
    Decision = PolicyDecision.Deny,
    Capability = ActionIntentKind.FileWrite,
    Target = "/workspace/production/code.py",
    DegradedCondition = "DeniedUnderFailClosed",
    DegradationReason = "Semantic supervisor unavailable for high-risk mutation"
}
```
