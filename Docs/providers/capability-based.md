# Capability-Based Provider Abstraction

Avoid giant universal provider interface with optional/null methods. Avoid provider-specific HTTP/error/model quirks leaking into routing core.

Minimum provider abstraction remains narrow: identity + basic chat execution.

Additional behavior through optional capabilities (model discovery, health probing, usage retrieval, rate-limit metadata, streaming transport support).

Capabilities like tool calling, vision, reasoning, context length, max output may belong to ProviderModelRoute / exposed model, not provider globally. Example: same provider exposes Model A (tools+vision) and Model B (tools, no vision). Do not flatten all model capabilities onto Provider.
