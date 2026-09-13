# Product Scope

ForgeGate: public open-source hybrid system.

Current implementation: OpenAI-compatible gateway ONLY.
Future optional: Agent Guard / control plane.

Gateway can inspect candidate tool calls before forwarding to client (interception), but does NOT own the execution runtime. This is a limitation — it is not equivalent to owning actual execution.

Internal bounded correction/replan loop is a future direction, not implemented.
