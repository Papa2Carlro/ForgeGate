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

## Current Architectural State

- Agent Guard integration: **PENDING**
- ToolCallInvocation exists but not wired to Agent Guard
- Policy decision flow: **NOT IMPLEMENTED**
- Structured event logging: PENDING

## Next Bounded Work Unit

**Slice 28: Tool Call Interception with Agent Guard Integration**

## Dependencies

- [x] Agent Guard domain model (commits 42bf4a6..246e3f0)
- [x] ToolCallInvocation record (commit 4af28b9)
- [x] ADR: Docs/decisions/agent-guard-capability-translation.md
- [x] Docs/supervision/implementation-order.md (Step 9)

## Key Architectural Constraints

1. **Tool calls must not bypass Agent Guard policy evaluation**
2. Agent Guard integration is a policy boundary, not transport/provider concern
3. Gateway must NOT depend on Agent Guard (conceptual split preserved)

## Deferred Work

- Persistence / Activity (Step 7 in implementation-order)
- Session Resolver (Step 8)
- Semantic policy escalation (Step 10)
- Completion Verifier (Step 11)
- Admin UI (Step 12)
