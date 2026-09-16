# Slice 28: Tool Call Interception with Agent Guard Integration

**Status:** OPEN
**Parent Milestone:** milestone-gateway-agent-guard
**Priority:** HIGH
**Created:** 2026-09-16

## Goal

Провести ToolCallInvocation через Agent Guard deterministic policy evaluation перед подальшим execution.

## Scope (Входить)

1. Non-streaming tool-call interception в ChatCompletionOrchestrator
2. Streaming tool-call interception в StreamingChatCompletionOrchestrator
3. Policy outcomes: Allow, Deny, RequireHumanApproval
4. Execution flow відповідно до policy decision
5. Regression coverage для non-tool-call requests

## Non-Goals (НЕ Входить)

- Persistence / Activity logging
- Semantic policy reasoner
- Correction loop / REPLAN
- Human approval UI
- Session Resolver
- Admin UI

## Dependencies

- [x] Agent Guard domain model (commits 42bf4a6..246e3f0)
- [x] ToolCallInvocation record (commit 4af28b9)
- [x] Streaming tool call accumulation (commits 670422a, 7aea420)
- [x] ADR: Docs/decisions/agent-guard-capability-translation.md
- [x] Docs/supervision/implementation-order.md (Step 9)

## Acceptance Criteria

1. Tool call створює ToolCallInvocation
2. Invocation передається в Agent Guard.Evaluate()
3. **Allow** → дозволяє execution flow
4. **Deny** → блокує execution, повертає PolicyDenied outcome
5. **RequireHumanApproval** → зупиняє execution, повертає HumanApprovalRequired outcome
6. Non-tool-call flow не змінюється
7. Unit/integration tests покривають всі три policy outcomes
8. Streaming behavior не обходить Agent Guard

## Evidence Sources

- `Docs/supervision/implementation-order.md`: Step 9 = "Tool interception + deterministic policy"
- `Docs/decisions/agent-guard-capability-translation.md`: ADR для Capability Translation
- `src/ForgeGate.Application/Chat/ToolCallInvocation.cs:10` — TODO для Agent Guard integration
- `src/ForgeGate.Api/Controllers/OpenAIStreamingChatCompletionResponse.cs:31` — TODO для streaming tool call design

## Certification Plan

```bash
# Focused tests
dotnet test ForgeGate.sln -c Release --filter "FullyQualifiedName~AgentGuard"

# Full suite
dotnet test ForgeGate.sln -c Release --no-build
# Expected: 326+ tests, 0 failed
```

## Open Questions

- Exact ActionIntent schema (deferred per ADR)
- Correction loop bounds (deferred per deferred-triggers.md)
