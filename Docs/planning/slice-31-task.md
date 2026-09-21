# Slice 31: Alert Formatter

**Status:** PROPOSED
**Parent Milestone:** milestone-gateway-agent-guard
**Priority:** HIGH
**Created:** 2026-09-22
**Slice:** 31 (following Slice 30 completion)

---

## Goal

Implement deterministic alert formatting from Agent Guard structured events, as mandated by ADR #9 (`agent-guard-capability-translation.md` §9):

> "User-facing alerts (System Alert) are derived from structured events, not independent UI constructs. The alert layer presents: `StructuredEvent → AlertFormatter → User-visible notification`"

---

## Scope (In Scope)

### Implementation
1. **Alert Formatter abstraction** — Define `IAlertFormatter` interface with `Format(AgentGuardEvent)` method
2. **Default formatter** — Implement `AgentGuardAlertFormatter` that produces deterministic, human-readable alert strings:
   - Success events: show capability, target, decision, Layer 3 finding
   - Failure events: show failure stage and reason
   - All events: include EventId for traceability
3. **Tests** — Verify deterministic output, all outcome types, Layer 3 preservation

---

## Non-Goals (Explicitly Deferred)

The following are OUT OF SCOPE for Slice 31:

- **UI rendering** — Text-only output, no HTML/markdown
- **Localization** — English only for MVP
- **Async emission** — Synchronous format only
- **Persistence** — No event store integration
- **Alert queue/batching** — No buffering or deduplication
- **System Alert injection** — Formatter produces string, does not deliver
- **Configuration** — No runtime config for alert format
- **Sensitive data redaction** — Deferred to persistence layer (Slice 35)

---

## Architectural Constraints

Must comply with existing canonical decisions:

1. **Structured event as source of truth** (`Docs/decisions/agent-guard-capability-translation.md` §8)
   - Formatter consumes AgentGuardEvent, does not create new data
   - Output is deterministic and reproducible from event input

2. **Alert is presentation of event** (`Docs/decisions/agent-guard-capability-translation.md` §9)
   - No alert logic outside event pipeline
   - Event remains canonical record; alert is derived representation

3. **Layer 3 result preservation** (Slice 29 contract)
   - Success events with Layer3Result must include risk finding in alert

---

## Closed Decisions Referenced

| Decision | Source | Status |
|----------|--------|--------|
| Structured event is source of truth | `agent-guard-capability-translation.md` §8 | LOCKED |
| System Alert is presentation of structured event | `agent-guard-capability-translation.md` §9 | LOCKED |
| Layer 3 produces evaluation finding | `layer-3-evaluation-result-boundary.md` | LOCKED |

---

## Acceptance Criteria

1. IAlertFormatter interface defined with Format method
2. AgentGuardAlertFormatter implements deterministic formatting
3. Success events include: capability, target, decision, Layer 3 finding (if present)
4. Failure events include: failure stage, failure reason
5. All events include EventId for traceability
6. No persistence, UI, or async logic in formatter
7. All existing tests continue to pass
8. New tests cover all outcome types and deterministic behavior

---

## Evidence Sources

- `Docs/decisions/agent-guard-capability-translation.md` §8-9
- `Docs/planning/slice-30-task.md` (event contract)
- `src/ForgeGate.Application/AgentGuard/AgentGuardEvent.cs`
- `src/ForgeGate.Application/AgentGuard/AgentGuardOutcomeType.cs`

---

## Implementation Notes

### Output Format
Text-based, structured for machine parsing and human readability:

```
[Agent Guard] Action approved: FileRead /workspace/foo.txt → Allow
  EventId: <guid>
  Decision: Allow
  Layer 3: No risk found
```

Failure format:
```
[Agent Guard] Normalization failed for action 'rm -rf /*' — Unknown command
```

### No State in Formatter
Formatter is stateless — same input always produces same output. No caching, no session context.

---

## Certification Plan

```bash
# Focused tests
dotnet test ForgeGate.sln -c Release --filter "FullyQualifiedName~AgentGuard"

# Full suite
dotnet test ForgeGate.sln -c Release
# Expected: All existing tests pass + new formatter tests
```

---

## Dependencies / Blockers

- [x] Structured event emission (Slice 30)
- [x] AgentGuardEvent with all required fields
- [x] ADR #8-9 (structured event, System Alert)

NO blockers. All prerequisite contracts are closed.
