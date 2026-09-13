# Public API

Public endpoint remains ordinary OpenAI-compatible:
POST /v1/chat/completions

No project identifiers in paths (e.g., /v1/{project}/chat/completions) unless explicitly decided later.
ForgeGate resolves session/workspace identity internally.
