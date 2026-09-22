# OpenAI API Compatibility Strategy

MVP external contract: GET /v1/models, POST /v1/chat/completions (Chat Completions family only). Supports non-streaming, streaming, system/user/assistant/tool messages, tool calls/tool_choice, model selection, common generation parameters, OpenAI-like error behavior.

Not required for MVP: /v1/responses, embeddings, images, audio, files, Assistants, batches, fine-tuning.

Internal architecture: OpenAI DTOs must NOT become core/domain contract. Protocol Adapter → canonical request → routing/policy/supervision → provider adapter. Later Responses API uses another adapter to same canonical model.

## Error Contract

### Agent Guard Policy Outcomes

When Agent Guard evaluates a tool call and produces a policy decision, the HTTP response reflects the decision:

| Policy Decision | HTTP Status | error.type | error.code | Semantics |
|-----------------|-------------|------------|------------|-----------|
| `Deny` | `403 Forbidden` | `agent_guard_denied` | `agent_guard_denied` | Action explicitly rejected by policy |
| `RequireHumanApproval` | `409 Conflict` | `agent_guard_requires_human_approval` | `agent_guard_requires_human_approval` | Action requires human authorization before execution |

Both outcomes preserve the existing `OpenAIError` envelope shape:

```json
{
  "error": {
    "message": "Agent Guard: tool call '{name}' denied by policy",
    "type": "agent_guard_denied",
    "code": "agent_guard_denied"
  }
}
```

The `message` field contains the sanitized upstream message from `ProviderFailure.SanitizedUpstreamMessage`.
