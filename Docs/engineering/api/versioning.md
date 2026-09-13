# Public API Versioning

OpenAI-compatible public API remains on OpenAI-style surface: /v1/... (GET /v1/models, POST /v1/chat/completions). ForgeGate-native control-plane/admin APIs use independent version namespace: /api/v1/.... Do NOT force ForgeGate-native versioning onto OpenAI-compatible surface. Do NOT assume both families share same version lifecycle.
