# Milestone: MVP Gateway + Agent Guard Foundation

**Status:** IN_PROGRESS
**Created:** 2026-09-16
**Goal:** OpenAI-compatible gateway з routing, streaming failover, та tool policy enforcement

## Completed Context

- **Slice 27 COMPLETED** — recovery anchor
- Gateway core: complete
- Routing (eligibility → quality → health → capacity): complete
- Streaming foundation (pre-commit/post-commit boundary): complete
- Agent Guard domain (capability registry, translator, evaluator): complete
- ToolCall extraction/streaming foundation: complete
- **Slice 28 COMPLETED** — Agent Guard integration for tool calls (commit 5090001)

## Current Architectural State

- Agent Guard integration: **COMPLETED** (commit 5090001)
- ToolCallInvocation wired to Agent Guard in both orchestrators
- Policy decision flow: **IMPLEMENTED** (Allow/Deny/RequireHumanApproval)
- Structured event logging: PENDING

## Forward Implementation Roadmap (Slices 30–39)

> **Roadmap Principle:** This forward roadmap is the current implementation order, not an immutable commitment. Completed slices remain historical; future slices may be reordered, split, merged, or replaced when new evidence or owner decisions change dependencies. Such changes must update the roadmap rather than trigger a full re-discovery cycle.

| Slice | Capability | Depends On | Status | Contract State | Key Output | Blocker (if any) |
|-------|-----------|------------|--------|---------------|------------|-----------------|
| 30 | Structured Event Emission | Slice 29 | READY | CLOSED | IAgentGuardEventEmitter, AgentGuardEvent type, NullEmitter, wired into AgentGuardService | — |
| 31 | Alert Formatter | Slice 30 | READY | CLOSED | AlertFormatter: StructuredEvent→User-visible notification per ADR#9 | — |
| 32 | Degraded Supervision Audit | Slice 30 | READY | PARTIALLY CLOSED | DegradedAuditEvent types (SemanticPolicyUnavailable, AllowedUnderFailOpen, etc.), recording logic | Event name/schema not fully finalized (per degraded-audit.md) |
| 33 | Correction Loop Framework | Slice 31 | BLOCKED | OPEN | ICorrectionLoop orchestrator stub, bounded retry framework | ① Correction prompt design ② Retry count/budget ③ Eligibility conditions (per backlog.md + deferred-triggers.md) |
| 34 | Session Context Integration | Slice 32 | BLOCKED | OPEN | Session-aware event enrichment, contextual evidence piping to Layer 3 | ① VS Code Custom Endpoint metadata inspection ② Stable conversation identifier design ③ Fallback fingerprint (per backlog.md) |
| 35 | Persistence Data Model | Slice 33 | BLOCKED | OPEN | EF Core entities for AgentGuardEvent, migration scripts, projection ownership | ① First durable entities/events schema ② Payload redaction/retention defaults (per backlog.md) |
| 36 | Semantic Escalation Framework | Slice 34 | BLOCKED | OPEN | ISemanticReasoner interface, escalation decision logic, threshold checking | ① Risk/suspicion score definition ② Escalation threshold values ③ Policy Reasoner model ④ Deterministic false-positive review (per mvp-status.md + triggers.md) |
| 37 | Completion Verification Framework | Slice 35 | BLOCKED | OPEN | ICompletionVerifier interface, claim detection logic, evidence-to-status mapping | ① Claim detection boundary ② Evidence-to-status rules ③ Client-facing correction behavior (per deferred-triggers.md) |
| 38 | Admin UI Foundation | Slice 37 | DEFERRED | OPEN | Admin panel skeleton, event query API, audit dashboard | Operational pain points review required before scope (per backlog.md) |
| 39 | Advanced Agent Guard Integration | Slice 38 | DEFERRED | OPEN | Hard execution-boundary integrations, cross-system coordination | Multiple unresolved: correction loop, session resolver, persistence, semantic reasoner all converge here (per deferred-triggers.md) |

### Dependency Chain

```
29 (Layer 3)
  ↓
30 (Event Emission) ──→ 31 (Alert Formatter) ──→ 32 (Degraded Audit)
  ↓                          ↓
33 (Correction Loop)      34 (Session Context) ──→ 35 (Persistence)
  ↓                          ↓                       ↓
36 (Semantic Escalation) ←──┴───────────────────────┘
  ↓
37 (Completion Verifier)
  ↓
38 (Admin UI) ── DEFERRED ──→ 39 (Advanced Integration) ── DEFERRED
```

### Critical Path Analysis

**Unblocked path (30→31→32):** Three consecutive READY slices building the event→alert→audit chain. These advance the "Structured event logging: PENDING" milestone item without requiring new owner decisions.

**First blocker (Slice 33):** Correction loop framework requires three unresolved decisions: correction prompt design, retry budget, and eligibility conditions. These are explicitly called out in `Docs/decisions/backlog.md` as "BEFORE Agent Guard runtime" decisions.

**Parallel blocker (Slice 34):** Session context integration requires VS Code metadata inspection and conversation identifier design. This is a prerequisite for meaningful cross-session Layer 3 evaluation but is independently blocked.

**Convergence point (Slice 35–37):** Persistence, semantic escalation, and completion verifier all depend on upstream decisions. The most constraining blocker is the semantic escalation threshold and reasoner model (Slice 36), which requires empirical review of deterministic false positives before it can be bounded.

### Key Owner Decisions Required

| Decision | Required For | Source |
|----------|-------------|--------|
| Correction loop bounds (prompt, retry count, eligibility) | Slice 33 | backlog.md, policy-outcome-replan-boundary.md |
| Session Resolver API (metadata, identifiers, fingerprint) | Slice 34 | backlog.md, layer-3-attribution-contract.md |
| Durable entity schema (events, redaction, retention) | Slice 35 | backlog.md |
| Semantic escalation threshold + reasoner model | Slice 36 | mvp-status.md, triggers.md, semantic-escalation.md |
| Claim detection boundary + evidence rules | Slice 37 | deferred-triggers.md, completion-verification.md |
| Admin UI operational pain points | Slice 38 | backlog.md |
