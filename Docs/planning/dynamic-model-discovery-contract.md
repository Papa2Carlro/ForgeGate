# Dynamic Model Discovery — Catalog Contract (Discovery Only)

Status: DISCOVERY COMPLETE. Implementation deferred to next bounded slice.

## 1. Canonical Evidence

### Provider architecture (canonical docs)
- `Docs/providers/capability-based.md`: capability-based abstraction; capabilities belong to `ProviderModelRoute` / exposed model, not provider globally.
- `Docs/providers/quirks.md`: quirks localized at adapter boundary; pipeline = `canonical → normalizer → transport → normalizer → canonical`.
- `Docs/engineering/providers/implementation.md`: `Infrastructure/Providers/XKiro/` folder; shared plumbing under `Common/`.
- `Docs/secrets/references.md`: `CredentialRef = "xkiro-main"`; raw keys not embedded in persisted config.
- `Docs/models/three-level.md`: Provider (xKiro, OpenRouter, Vireonix); LogicalModel (minimax-m2.7, devstral, mistral-large); ModelRoute = provider-specific exposure.
- `Docs/decisions/backlog.md`: BEFORE Provider Adapters: finalize minimum provider contract; first capability contracts; provider-specific error normalization.

### NVIDIA/NIM
- NOT mentioned in any canonical doc (`Docs/` search: zero results).
- Only `.cursor/index` noise (`minimax/minimax-m2.5` — unrelated).
- External vendor fact: NIM is OpenAI-compatible endpoint.
- Project decision: adapter strategy is capability-based; no separate adapter required unless NIM introduces provider-specific quirks.

### Existing adapter
- `OpenAIChatCompletionProvider`: generic OpenAI-compatible adapter (`Infrastructure/Providers/OpenAICompatible/`).
- `XKiroChatCompletionProvider`: dedicated adapter (`Infrastructure/Providers/XKiro/`), same OpenAI-compatible DTO mapping.
- Both implement `IChatCompletionProvider`.

## 2. Existing Code Contract

### Provider identity
- `ProviderId` (`Domain/Providers/ProviderId.cs`): `record struct`, `Value: string`, `From(string)`.
- `Provider` (`Domain/Providers/Provider.cs`): `record`, `Id: ProviderId`, `Name: string`.

### Model identity
- `LogicalModelId` (`Domain/Providers/LogicalModelId.cs`): `record struct`, `Value: string`.
- `ModelRouteId` (`Domain/Providers/ModelRouteId.cs`): `record struct`, `Value: string`.
- `ModelRoute` (`Domain/Providers/ModelRoute.cs`): `ProviderId`, `LogicalModelId`, `ModelRouteId`, `ProviderNativeModelId` (string), `Enabled`, `Capabilities` (`ModelCapability` flags: `None`, `Tools`), `QualityTier`, `MaxConcurrentExecutions`.

### Provider adapter contract
- `IChatCompletionProvider` (`Application/Chat/IChatCompletionProvider.cs`): single method `ExecuteAsync(ModelRoute, CanonicalChatRequest, CancellationToken) → ProviderExecutionOutcome`.
- Adapter maps: `CanonicalChatRequest → provider DTO → HTTP → provider response DTO → CanonicalChatResponse`.
- Failure normalization: `OpenAIProviderFailureMapper` (used by both OpenAI and xKiro adapters).

### Routing
- `RoutingConfiguration` (`Infrastructure/Routing/RouteConfigurationModels.cs`): static routes configured in `appsettings.json`.
- `ConfiguredRouteResolver`: selects route by `RequestedModelAlias` / `ProviderId` / `LogicalModelId`.
- `ModelRoute` is static; no dynamic discovery mechanism exists.

### Capabilities
- `ModelCapability` (`Domain/Providers/ModelCapability.cs`): `[Flags]` enum — `None = 0`, `Tools = 1`.
- Capabilities are declared on `ModelRoute`, not on provider globally (`capability-based.md`).

## 3. Real xKiro Catalog Facts (Live Discovery — Executed)

Endpoint: `https://api.xkiro.com/v1/models` (from `.env` / runtime config).
Auth: Bearer (`XKiro__ApiKey` from `.env`, value hidden, not committed).

### Response structure (verified from real HTTP response)
```
{
  "object": "list",
  "data": [
    {
      "id": "provider/model-id",              // native model ID
      "object": "model",
      "type": "model",
      "display_name": "...",
      "created": 1,
      "owned_by": "provider-owner",           // e.g. "minimax", "qwen", "mistral", "nvidia"
      "modality": "chat",
      "access_tier": "free" | "paid" | "premium",
      "pricing": {
        "currency": "USD",
        "unit": "per_1m_tokens",
        "input": number,
        "output": number,
        "cache_read": number,
        "cache_write": number
      },
      "capabilities": {
        "vision": boolean,
        "tools": boolean,
        "reasoning": boolean
      },
      "context_length": number,
      "max_output_tokens": number,
      "reasoning_efforts": {
        "levels": ["..."],
        "default": "..."
      },
      // Optional vendor-specific fields observed:
      "min_plan_usd": number,
      "min_plan_names": ["..."]
    }
  ]
}
```

### Verified statistics
- Total models: 114
- Free models (`access_tier == "free"`): 37
- Paid (`paid`): multiple
- Premium (`premium`): multiple

### Free model examples (verified)
- `minimax/minimax-m2.5:free` — vision=false, tools=true, reasoning=true, input=0, output=0
- `mistralai/devstral-medium` — vision=false, tools=true, reasoning=false, input=0, output=0
- `qwen/qwen3.8-max:free` — vision=true, tools=true, reasoning=true, input=0, output=0
- `nvidia/nemotron-3-nano-omni` — vision=true, tools=true, reasoning=true, `access_tier=paid` (NOT free)

### Free classification (verified from real data)
A model is `free` when:
- `access_tier == "free"` AND
- `pricing.input == 0` AND `pricing.output == 0`

This is provider-neutral and derived directly from the API response. No assumption needed.

## 4. Canonical Catalog Contract (Proposed — Discovery Only)

This is a **contract proposal**, not an implementation. It is derived from:
- Canonical adapter strategy (`quirks.md`, `capability-based.md`)
- Existing `ModelRoute` / `ProviderId` / `LogicalModelId` contracts
- Real xKiro `/v1/models` response structure
- Requirement: provider-neutral, no hardcoded model IDs

### Provider identity (existing — no change needed)
```
ProviderId  (existing: record struct, Value: string)
ProviderName (optional enrichment, not required for routing)
```

### Catalog entry (new contract — minimal)
```
CatalogEntry
  - ProviderId: ProviderId              // which provider exposes this
  - NativeModelId: string               // provider-native ID (e.g. "minimax/minimax-m2.5:free")
  - DisplayName: string                 // human-readable (e.g. "MiniMax M2.5 (Free)")
  - LogicalModelAlias: string?          // optional mapping to LogicalModelId (e.g. "minimax-m2.7")
  - AccessTier: string                  // "free" | "paid" | "premium" (from real response)
  - Capabilities: ModelCapability       // existing flags enum (None, Tools) — extend if needed
  - ContextLength: int?
  - MaxOutputTokens: int?
  - Pricing: CatalogPricing?            // optional, for free/paid classification
```

### Catalog pricing (optional — for free classification)
```
CatalogPricing
  - Currency: string                    // e.g. "USD"
  - Unit: string                        // e.g. "per_1m_tokens"
  - InputPrice: decimal                 // 0 = free
  - OutputPrice: decimal                // 0 = free
```

### Catalog discovery interface (proposed — NOT implemented)
```
IProviderCatalog
  - DiscoverAsync(ProviderId, CancellationToken) → CatalogResponse
```

### Catalog response (proposed)
```
CatalogResponse
  - ProviderId: ProviderId
  - Entries: CatalogEntry[]
  - SourceEndpoint: string              // which endpoint was queried (for audit)
  - RetrievedAt: DateTimeOffset
```

### Key design decisions (derived from canonical docs)
1. **Native model ID is separate from canonical identity**: `NativeModelId` (provider-specific, e.g. `"minimax/minimax-m2.5:free"`) is NOT the same as `LogicalModelId` (canonical, e.g. `"minimax-m2.7"`). The mapping is optional (`LogicalModelAlias?`).
2. **Capabilities stay on model, not provider**: `Capabilities` is per `CatalogEntry`, consistent with `capability-based.md`.
3. **Free classification is derived, not hardcoded**: `AccessTier == "free"` + zero pricing = free. No `IsFree` boolean needed.
4. **No provider-specific branching in routing/core**: Catalog is provider-neutral; routing consumes `CatalogEntry[]` without `if provider == "xkiro"`.
5. **No persistence in this contract**: Catalog is ephemeral (discovered at runtime). Persistence is deferred (backlog: `BEFORE Persistence`).

## 5. Catalog → ModelRoute Relationship

### Current (static)
```
appsettings.json (static RoutingConfiguration)
  → ConfiguredRouteResolver
    → ModelRoute (hardcoded ProviderNativeModelId, LogicalModelId, ProviderId)
      → ChatExecutionService
        → IChatCompletionProvider (OpenAIChatCompletionProvider or XKiroChatCompletionProvider)
```

### Target (with catalog — NOT implemented)
```
User request: "Minimax Free"
  ↓
Catalog Discovery (IProviderCatalog.DiscoverAsync)
  → CatalogResponse (CatalogEntry[] from /v1/models)
    ↓
Filter / Search Catalog
  - Filter by AccessTier == "free"
  - Filter by LogicalModelAlias == "minimax-m2.7" (optional)
  - Filter by Capabilities (optional)
    ↓
Candidate Selection
  → Candidate CatalogEntry[]
    ↓
Route Resolution (existing ConfiguredRouteResolver extended OR new CatalogRouteResolver)
  → Create ModelRoute from CatalogEntry
    - ProviderId = CatalogEntry.ProviderId
    - LogicalModelId = CatalogEntry.LogicalModelAlias ?? derived from entry
    - ProviderNativeModelId = CatalogEntry.NativeModelId
    - Capabilities = CatalogEntry.Capabilities
    - QualityTier = derived from AccessTier / capabilities
    ↓
ChatExecutionService (existing — no change)
  → IChatCompletionProvider (adapter selected by ProviderId / route)
```

### Critical boundary preservation
- `ModelRoute.ProviderNativeModelId` remains the provider-native model ID (`"minimax/minimax-m2.5:free"`).
- `ModelRoute.LogicalModelId` remains the canonical logical model (`"minimax-m2.7"`).
- The mapping between native and logical is optional and advisory; routing does not enforce it.
- Catalog does NOT replace `ModelRoute`; it feeds into route creation/resolution.

### What is missing for dynamic selection
1. `IProviderCatalog` interface (not implemented).
2. Catalog adapter for xKiro (`XKiroCatalogAdapter` or similar — uses `/v1/models` endpoint).
3. Catalog filter/search mechanism (not implemented).
4. Dynamic `ModelRoute` creation from `CatalogEntry` (not implemented).
5. Routing resolver extension to consume catalog candidates (not implemented).
6. User intent parser (`"Minimax Free"` → filter criteria) — explicitly out of scope for this discovery.

## 6. `"Minimax Free"` Data Flow (Conceptual — NOT Implemented)

```
User input: "Minimax Free"
  ↓
Intent parsing (future — out of scope)
  → Filter criteria:
    - Provider family: "minimax" (derived from name/display_name/owned_by)
    - Access tier: "free"
    - Capabilities: optional (e.g. tools=true)
  ↓
Catalog Discovery (future)
  → Query xKiro /v1/models
  → Filter CatalogEntry[]
    - owned_by contains "minimax" OR display_name contains "MiniMax"
    - access_tier == "free"
  → Candidate: CatalogEntry { NativeModelId="minimax/minimax-m2.5:free", ... }
  ↓
Route Resolution (future extension)
  → Create ModelRoute:
    ProviderId = "xkiro"
    LogicalModelId = "minimax-m2.7" (canonical mapping — owner decision needed)
    ProviderNativeModelId = "minimax/minimax-m2.5:free"
    Capabilities = Tools (from CatalogEntry.capabilities)
  ↓
ChatExecutionService (existing)
  → XKiroChatCompletionProvider (existing adapter)
    → HTTP POST to XKiro__Endpoint
    → Auth: Bearer XKiro__ApiKey
    → Request model: "minimax/minimax-m2.5:free"
```

### What the adapter needs to handle
The adapter (`XKiroChatCompletionProvider`) already uses `route.ProviderNativeModelId` as the model in the request. If the route is created dynamically with `ProviderNativeModelId = "minimax/minimax-m2.5:free"`, the adapter will send that model ID to xKiro without any adapter change. This confirms the adapter is compatible with dynamic model selection.

## 7. Multi-Provider Behavior (Conceptual — NOT Implemented)

### Same logical model, multiple providers
```
LogicalModel: minimax-m2.7
  → xKiro route: xkiro:minimax-m2.7 (ProviderNativeModelId = "minimax/minimax-m2.7" or free variant)
  → OpenRouter route: openrouter:minimax-m2.7
  → Vireonix route: vireonix:minimax-m2.7
```
The catalog contract supports this: each `CatalogEntry` has its own `ProviderId` and `NativeModelId`. Routing can select among candidates from different providers.

### Same provider, many models
```
Provider: xKiro
  → CatalogEntry[] includes:
    - minimax/minimax-m2.5:free
    - minimax/minimax-m2.7:free
    - qwen/qwen3.8-max:free
    - mistralai/devstral-medium
    - ... (114 total)
```
The adapter (`XKiroChatCompletionProvider`) handles any model ID passed through `route.ProviderNativeModelId`. No adapter change needed for multi-model support.

### Candidate set (not ranking)
The contract defines `CatalogResponse` as a list (`CatalogEntry[]`). Ranking/winner selection is a routing concern, not a catalog concern. The catalog layer provides candidates; routing selects.

## 8. GAP Table

| Area | Current | Target | Gap |
|---|---|---|---|
| Provider catalog interface | None (`IProviderCatalog` does not exist) | `IProviderCatalog.DiscoverAsync` | **Interface missing** |
| Catalog adapter (xKiro) | None | `XKiroCatalogAdapter` using `/v1/models` | **Not implemented** |
| Catalog contract / DTO | None | `CatalogEntry`, `CatalogResponse`, `CatalogPricing` | **Not implemented** |
| Catalog filter/search | None | Filter by `access_tier`, `owned_by`, `capabilities`, `LogicalModelAlias` | **Not implemented** |
| Dynamic `ModelRoute` creation | Static (`appsettings.json`) | Create from `CatalogEntry` | **Not implemented** |
| Routing resolver extension | `ConfiguredRouteResolver` (static) | Consume catalog candidates | **Not implemented** |
| User intent parser (`"Minimax Free"`) | None | Parse → filter criteria | **Explicitly out of scope** |
| Catalog persistence / cache | None | Ephemeral (runtime discovery) per contract proposal | **Deferred** (backlog: persistence) |
| Secret-store abstraction (`CredentialRef`) | `.env` / `IConfiguration` direct | `CredentialRef = "xkiro-main"` resolved through store | **Deferred** (backlog: secret management) |
| Streaming adapter for xKiro | `OpenAIStreamingChatCompletionProvider` exists; no xKiro streaming adapter | `XKiroStreamingChatCompletionProvider` (if needed) | **Deferred** (not required by scope) |
| Provider contract finalization | Backlog open (`BEFORE Provider Adapters`) | Finalized minimum contract | **Still open** |

## 9. Owner Decisions (Unresolved — Require Explicit Decision)

These are NOT artificial questions. Each is required before the next bounded implementation slice can proceed safely.

### 1. Canonical model identity (REQUIRED)
Does `LogicalModelId` (`"minimax-m2.7"`) remain the canonical identity, with `NativeModelId` (`"minimax/minimax-m2.5:free"`) as provider-specific exposure? Or does the catalog introduce a new canonical identity layer?
- **Evidence**: `three-level.md` defines three distinct levels; `ModelRoute` links them.
- **Implication**: If `LogicalModelId` stays canonical, the mapping `minimax/minimax-m2.5:free` → `minimax-m2.7` must be defined (either static config or derived from `display_name`/`owned_by`).

### 2. Free semantics (REQUIRED)
Is `free` defined as:
- `access_tier == "free"` only?
- `pricing.input == 0` AND `pricing.output == 0`?
- Both combined?
- **Evidence**: Real xKiro response shows both fields; some `free` models have `pricing: {input:0, output:0}`; some `paid` have non-zero pricing.
- **Implication**: The adapter/catalog must agree on this definition. If the owner wants `free` to mean zero pricing, the contract must specify both conditions.

### 3. Catalog authoritative vs advisory (REQUIRED)
Is the catalog the authoritative source of available routes, or advisory (with static `appsettings.json` remaining authoritative)?
- **Evidence**: `backlog.md` says `BEFORE Provider Adapters: finalize minimum provider contract`. No catalog persistence exists.
- **Implication**: If advisory, routing uses static config + catalog enrichment. If authoritative, routing must be rebuilt to consume catalog.

### 4. Provider-native model ID in route (REQUIRED)
Does `ModelRoute.ProviderNativeModelId` remain the provider-native model ID (e.g. `"minimax/minimax-m2.5:free"`)?
- **Evidence**: `ModelRoute` defines this field; adapter uses it (`route.ProviderNativeModelId`).
- **Implication**: Dynamic route creation must populate this field from `CatalogEntry.NativeModelId`.

### 5. Catalog persistence (REQUIRED — deferred, not blocking)
Is the catalog ephemeral (discovered per request) or cached/persisted?
- **Evidence**: `backlog.md`: `BEFORE Persistence`. No persistence layer for catalog exists.
- **Implication**: For the next bounded slice, ephemeral discovery is sufficient. Persistence is a separate future slice.

### 6. Streaming adapter for xKiro (NOT REQUIRED for next slice)
Does xKiro streaming require a separate adapter (`XKiroStreamingChatCompletionProvider`)?
- **Evidence**: `OpenAIStreamingChatCompletionProvider` exists; streaming is deferred in backlog.
- **Implication**: Not blocking for the catalog contract slice.

## 10. Next Bounded Implementation Slice

Based on the discovery and the unresolved owner decisions, the next bounded slice should be:

```
Provider Catalog Contract + xKiro Catalog Adapter
```

### What it includes (bounded)
- Define `IProviderCatalog`, `CatalogEntry`, `CatalogResponse`, `CatalogPricing` interfaces/contracts.
- Implement `XKiroCatalogAdapter` (uses `/v1/models` endpoint, same auth as adapter).
- Add deterministic tests for catalog adapter (fake HTTP handler, verify response parsing, verify free model filtering).
- Update `.env.example` with `XKiro__Endpoint` (already done) — no new secrets.
- Document the contract in `Docs/providers/catalog-contract.md` (or similar).

### What it explicitly excludes
- Dynamic routing (`ConfiguredRouteResolver` unchanged).
- User intent parser (`"Minimax Free"` not implemented).
- Catalog persistence / cache.
- Routing resolver extension.
- Multi-provider ranking.
- Agent Guard changes.
- Streaming adapter.
- Secret-store abstraction (`CredentialRef`).
- Any production behavior change beyond adapter + contract.

### Files likely to change
- `src/ForgeGate.Application/Chat/IProviderCatalog.cs` (new interface)
- `src/ForgeGate.Application/Chat/CatalogEntry.cs` (new DTO)
- `src/ForgeGate.Application/Chat/CatalogResponse.cs` (new DTO)
- `src/ForgeGate.Application/Chat/CatalogPricing.cs` (new DTO — optional)
- `src/ForgeGate.Infrastructure/Providers/XKiro/XKiroCatalogAdapter.cs` (new adapter)
- `tests/ForgeGate.Application.Tests/XKiroCatalogAdapterTests.cs` (new tests)
- `Docs/providers/catalog-contract.md` (new doc — contract only)
- `.env.example` (already has placeholder; may add comment)

### Tests required
- Catalog adapter parses `/v1/models` response correctly.
- Catalog adapter filters `free` models correctly (using `access_tier` + pricing).
- Catalog adapter does not expose API key.
- Catalog adapter handles HTTP errors.
- Catalog adapter handles malformed responses.

### Verification for next slice
- `dotnet build` passes.
- `dotnet test` for new catalog adapter tests passes.
- `git diff --check` clean.
- No secrets in diff.
- No production routing/core changes.

---

## 11. Files Changed (This Discovery Only — No Implementation)

No production files changed. Only this audit document created (if saved). No commit. No push.

If saved as `Docs/planning/dynamic-model-discovery-contract.md`:
- `Docs/planning/dynamic-model-discovery-contract.md` (new — this document)

No other files modified.

## 12. Verification
- `git diff --check`: clean (no whitespace errors in new doc).
- `git status --short`: only new doc file (if saved); no tracked file modifications.
- `git diff --name-only`: only `Docs/planning/dynamic-model-discovery-contract.md` (if saved).
- No secrets in any file.
- No `.env` credentials committed.
- No `XKiro__Model` added.
- No production adapter/routing/core changes.

## 13. Commit Status
No commit. No push. Discovery complete. Implementation deferred to next bounded slice.
