# Domain / Application / Infrastructure / API / Admin

Domain: core business concepts/invariants without infrastructure.
Application: use cases; orchestration of domain capabilities; ports/contracts; application-level decisions.
Infrastructure: provider HTTP integrations; EF Core/PostgreSQL; secret-store implementations; external adapters.
Api: HTTP/OpenAI-compatible protocol layer; transport validation; DI composition/startup; OpenAPI.
Admin: React/TypeScript operational control-plane UI.

Do NOT force every concept into Domain if it is actually application workflow.
