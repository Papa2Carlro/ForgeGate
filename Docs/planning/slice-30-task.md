# Slice 30: Structured Event Emission

**Status:** PROPOSED
**Parent Milestone:** milestone-gateway-agent-guard
**Priority:** HIGH
**Created:** 2026-09-21
**Slice:** 30 (following Slice 29 completion)

---

## Goal

Establish the Agent Guard structured event emission contract: define the event type(s), create an emitter interface, wire emission into AgentGuardService so every pipeline outcome (success or failure) produces a structured event. This makes the "Structured event logging: PENDING" milestone item actionable without committing to persistence or alert formatting.

---

## Context

`AgentGuardService.Evaluate()` currently returns `AgentGuardResult` but emits no events. ADR #8 (`agent-guard-capability-translation.md`) mandates:

> Every Agent Guard intervention produces a structured event:
> - Translation decision (raw → normalized)
> - Policy evaluation result (allow/deny/review)
> - Execution outcome (success/failure/rejected)
> - Correction round (if applicable)
>
> These events are append-only, auditable, and queryable.

An `AgentGuardEvent` skeleton exists but is incomplete (only Observation + NormalizationResult) and is never emitted.

---

## Scope (In Scope)

### Implementation
1. **Event type definition** — Define the in-memory structured event that captures a complete Agent Guard pipeline intervention:
   - Common fields: EventId, Timestamp, CorrelationId
   - Pipeline fields: AgentAction source, NormalizationResult, PolicyDecision, Layer3EvaluationResult, final outcome status
   - Event type discriminator (enum or record subtype) distinguishing: Normalized, PolicyEvaluated, Failed
   - Representation is OPEN — enum + sealed class, or sealed record hierarchy with type tag, or single record with discriminant — agent chooses the minimal representation compatible with existing patterns

2. **Emitter interface** — Define minimal emission interface:
   ```csharp
   public interface IAgentGuardEventEmitter
   {
       void Emit(AgentGuardEvent @event);
   }
   ```
   No event store, no persistence, no async/batching — just the emission contract.

3. **Default null emitter** — Provide `NullAgentGuardEventEmitter : IAgentGuardEventEmitter` that discards all events (allows optional emission).

4. **Wire into AgentGuardService** — Make emission optional via constructor injection:
   - On success (Allow/Deny/RequireHumanApproval): emit PolicyEvaluated event
   - On failure (Normalization/Translation/PolicyEvaluation): emit Failed event
   - When emitter is null → no emission (backward compatible)

5. **Tests** — Verify:
   - Event emitted on successful pipeline (contains PolicyDecision, Layer3Result, Capability)
   - Event emitted on normalization failure (contains failure reason)
   - Event emitted on translation failure
   - Event emitted on policy evaluation failure
   - No event emitted when emitter is null (backward compatibility)
   - Event contains correct CorrelationId matching the AgentAction

---

## Non-Goals (Explicitly Deferred)

The following are OUT OF SCOPE for Slice 30:

- **Persistence** — No event store, no database, no durable storage (Step 7)
- **Alert formatting** — No AlertFormatter, no System Alert derivation (deferred)
- **Degraded audit events** — No SemanticPolicyUnavailable, CompletionVerifierUnavailable, etc. (deferred per `degraded-audit.md`)
- **Correlation tracking across subsystems** — Only internal CorrelationId; no cross-process correlation
- **Event replay / query** — No read path for events
- **Async/batched emission** — Synchronous emit only
- **Event versioning/schema evolution** — Not needed for in-memory contract
- **Admin UI integration** — Deferred
- **Multi-event burst** — Single event per Evaluate() call (one intervention = one event)

---

## Architectural Constraints

Must comply with existing canonical decisions:

1. **Structured event as source of truth** (`Docs/decisions/agent-guard-capability-translation.md` §8)
   - Event is the canonical record; not logs, not UI state
   - Represents "what happened" not "what to do"

2. **Layer 3 result must be included** (Slice 29 contract)
   - PolicyEvaluated event must carry Layer3EvaluationResult

3. **Failure events preserve context**
   - NormalizationFailed event must carry RawAction and FailureReason
   - TranslationFailed event must carry Capability and FailureReason
   - PolicyEvaluationFailed event must carry Capability and FailureReason

4. **No breaking changes to existing code**
   - AgentGuardService constructor must remain backward compatible (emitter is optional)
   - Existing tests must continue to pass

---

## Closed Decisions Referenced

| Decision | Source | Status |
|----------|--------|--------|
| Structured event is source of truth | `agent-guard-capability-translation.md` §8 | LOCKED |
| System Alert is presentation of event | `agent-guard-capability-translation.md` §9 | LOCKED |
| PolicyDecision = Allow/Deny/RequireHumanApproval | `policy-outcome-replan-boundary.md` | LOCKED |
| Layer 3 produces evaluation finding | `layer-3-evaluation-result-boundary.md` | LOCKED |

---

## Deferred Items (Preserved)

The following remain OPEN and are NOT addressed by Slice 30:

- Exact event subtype taxonomy (how many types, what names)
- Durable payload version schema
- Alert formatter contract
- Degraded supervision event types
- Event query/read API
- Event retention/expiration policy
- Cross-subsystem correlation protocol

---

## Acceptance Criteria

1. AgentGuardEvent has all required fields from ADR #8 (PolicyDecision or failure reason)
2. IAgentGuardEventEmitter interface defined with Emit method
3. NullAgentGuardEventEmitter provided (default when no emitter registered)
4. AgentGuardService emits event on every Evaluate() exit path (success or failure)
5. Event carries Layer3EvaluationResult when present
6. Event carries correct PolicyDecision or FailureStage/FailureReason
7. Backward compatibility: existing code without emitter continues to work
8. All existing tests continue to pass
9. New tests cover all emission paths (4 success + 3 failure scenarios)
10. No persistence, no alerts, no query API implemented

---

## Evidence Sources

- `Docs/decisions/agent-guard-capability-translation.md` §8-9 (Structured event, System Alert)
- `Docs/planning/milestone-gateway-agent-guard.md` ("Structured event logging: PENDING")
- `src/ForgeGate.Application/AgentGuard/AgentGuardEvent.cs` (existing skeleton)
- `src/ForgeGate.Application/AgentGuard/AgentGuardService.cs` (current pipeline)
- `src/ForgeGate.Domain/AgentGuard/AgentGuardResult.cs` (result carrying decision)

---

## Implementation Notes

### Event Structure Options

The exact representation is OPEN. Three viable patterns:

**Option A: Single record with discriminant**
```csharp
public sealed record AgentGuardEvent(
    Guid EventId,
    DateTime CreatedAt,
    Guid CorrelationId,
    AgentAction Source,
    AgentGuardOutcome Outcome); // sealed class with PolicyDecision or Failure

public sealed class AgentGuardOutcome
{
    public PolicyDecision? Decision { get; init; }
    public Layer3EvaluationResult? Layer3Result { get; init; }
    public ActionIntentKind? Capability { get; init; }
    public string? FailureStage { get; init; }
    public string? FailureReason { get; init; }
}
```

**Option B: Sealed hierarchy**
```csharp
public abstract record AgentGuardEvent(Guid EventId, DateTime CreatedAt, Guid CorrelationId, AgentAction Source);
public sealed record PolicyEvaluatedEvent(...) : AgentGuardEvent;
public sealed record PipelineFailedEvent(...) : AgentGuardEvent;
```

**Option C: Enrich existing AgentGuardEvent**
Add fields to existing record; add OutcomeType enum for discrimination.

Agent chooses the pattern that best fits existing code style. Options A and C are preferred for minimal change surface.

### Emission Integration

AgentGuardService.Evaluate() should emit at the end of each exit path:
```csharp
public AgentGuardResult Evaluate(AgentAction action)
{
    // ... existing stages 1-3 ...
    
    // Stage 4: Layer 3 evaluation
    // Stage 5: Build result and emit
    var result = AgentGuardResult.Success(...);
    _emitter?.Emit(BuildEvent(result));
    return result;
}
```

### Test Strategy

Use NullAgentGuardEventEmitter as default. For emission tests, inject a TrackingEventEmitter that records events for assertion.

---

## Certification Plan

```bash
# Focused tests
dotnet test ForgeGate.sln -c Release --filter "FullyQualifiedName~AgentGuard"
dotnet test ForgeGate.sln -c Release --filter "FullyQualifiedName~Event"

# Full suite
dotnet test ForgeGate.sln -c Release --no-build
# Expected: All existing tests pass + new event emission tests
```

---

## Dependencies / Blockers

- [x] Layer 3 evaluation seam (Slice 29)
- [x] AgentGuardResult with Layer3Result
- [x] ADR #8 (structured event as source of truth)

NO blockers. All prerequisite contracts are closed.
