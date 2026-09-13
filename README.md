# ForgeGate

**An open-source LLM gateway and agent policy firewall.**

ForgeGate sits between your applications and LLM providers, providing a unified OpenAI-compatible API with built-in routing, failover, and deterministic policy enforcement for dangerous agent actions.

## Current Status

🚧 **Early development** — repository scaffolding and foundation only. No gateway features are implemented yet.

## Intended Architecture

```
┌─────────────┐     ┌──────────────┐     ┌──────────────────┐
│   Clients    │────▶│  ForgeGate   │────▶│  LLM Providers   │
│  (OpenAI SDK)│     │  API Gateway │     │  (OpenAI, etc.)  │
└─────────────┘     └──────────────┘     └──────────────────┘
                          │
                    ┌─────┴──────┐
                    │  Policies  │
                    │  Routing   │
                    │  Audit Log │
                    └────────────┘
```

- **Domain** — Core types: providers, routes, policies, model aliases.
- **Application** — Use-case orchestration, service interfaces.
- **Infrastructure** — Persistence (EF Core / PostgreSQL), external integrations.
- **API** — ASP.NET Core HTTP layer, OpenAI-compatible endpoints.
- **Admin** — React + TypeScript + Vite management UI.

## Local Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0+ |
| Node.js | 22+ |
| Docker | 24+ |
| Docker Compose | v2+ |

## Getting Started

### Start PostgreSQL

```bash
cp .env.example .env   # edit if needed
docker compose up -d
```

### Build & Test

```bash
dotnet build
dotnet test
```

### Run the API

```bash
dotnet run --project src/ForgeGate.Api
```

### Admin UI

```bash
cd web/ForgeGate.Admin
npm install
npm run dev
```

## ⚠️ Security

**Never commit provider credentials, API keys, connection strings with passwords, or any secrets.**

Use `.env` (git-ignored) for local development. Production secrets should be managed through environment variables or a secrets manager.

## License

[MIT](LICENSE)
