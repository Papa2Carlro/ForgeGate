# OpenAI API Compatibility Strategy

MVP external contract: GET /v1/models, POST /v1/chat/completions (Chat Completions family only). Supports non-streaming, streaming, system/user/assistant/tool messages, tool calls/tool_choice, model selection, common generation parameters, OpenAI-like error behavior.

Not required for MVP: /v1/responses, embeddings, images, audio, files, Assistants, batches, fine-tuning.

Internal architecture: OpenAI DTOs must NOT become core/domain contract. Protocol Adapter → canonical request → routing/policy/supervision → provider adapter. Later Responses API uses another adapter to same canonical model.
