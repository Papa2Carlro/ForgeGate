# Real Provider Tests

Tests calling real external providers must be: explicitly opt-in; separated from normal deterministic CI; clearly identified as integration/live tests; safe regarding credentials/rate limits. Standard test suite must not depend on free provider availability.

## Explicit Opt-In Gate

Live smoke is disabled by default. It runs only when:

```bash
FORGEGATE_RUN_REAL_PROVIDER_SMOKE=true
```

Without this environment variable, `RealProviderSmokeTests` is skipped (not failed).

## Required Environment Variables (runtime only)

```
FORGEGATE_RUN_REAL_PROVIDER_SMOKE=true
OpenAI__Endpoint=https://api.openai.com/v1/chat/completions
OpenAI__ApiKey=<runtime-secret>
```

- `OpenAI__ApiKey` maps to `OpenAI:ApiKey` in .NET configuration.
- `OpenAI__Endpoint` maps to `OpenAI:Endpoint`.
- Never commit `.env`, `appsettings*.json`, or any file containing real credentials.

## Running the Live Smoke

```bash
FORGEGATE_RUN_REAL_PROVIDER_SMOKE=true \
OpenAI__Endpoint=https://api.openai.com/v1/chat/completions \
OpenAI__ApiKey="$OPENAI_API_KEY" \
dotnet test tests/ForgeGate.IntegrationTests/ForgeGate.IntegrationTests.csproj \
  --filter "FullyQualifiedName~RealProviderSmokeTests"
```

## xKiro Live Smoke (first real provider)

Requires both the gate AND the xKiro runtime key:

```bash
FORGEGATE_RUN_REAL_PROVIDER_SMOKE=true \
XKiro__Endpoint=https://api.xkiro.com/v1/chat/completions \
XKiro__ApiKey="$XKIRO_API_KEY" \
dotnet test tests/ForgeGate.IntegrationTests/ForgeGate.IntegrationTests.csproj \
  --filter "FullyQualifiedName~XKiroRealProviderSmokeTests"
```

- `XKiro__ApiKey` maps to `XKiro:ApiKey`.
- `XKiro__Endpoint` maps to `XKiro:Endpoint`.
- The adapter (`XKiroChatCompletionProvider`) uses the same OpenAI-compatible request/response shape.
- No real key committed; `.env.example` contains only empty placeholders.

## Security Rules

- Secrets only via runtime environment/configuration.
- No `.env` credentials committed.
- No test fixtures with real keys.
- Live smoke is NOT part of deterministic CI.
