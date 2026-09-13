# ADR 0001 — View-scoped Zustand hybrid for UI state

**Date:** 2026-08-06  
**Status:** Accepted  
**Provenance:** Same decision as Doc Hub ADR 0007 (`doc-hub/Docs/ADR/0007-view-scoped-zustand-hybrid.md`) / Finance Launcher ADR 0005 (site numbering starts at 0001).

## Context

Landing / marketing SPAs accumulate page roots that own every toggle and fan out `value` + `setValue` pairs into sections. That makes pages hard to split and cross-product copy-paste painful.

We need a **portable** rule for where ephemeral **page / view UI** state lives versus content / fetch state — without a global god-store.

`dochub-site` today is mostly local `useState` in pages and a flat `src/components/*.tsx` tree. That is fine for tiny leaves. New heavy pages (Benchmark tabs, future docs/FAQ filters, admin) migrate toward this ADR.

## Decision

Use a **hybrid**:

1. **View UI / filters / tabs** shared by siblings — view-scoped **Zustand** store created with a **factory** (`createXStore()`), held in a React **Provider** for the lifetime of the mounted page. Consumers use **selectors** (`useStore(store, selector)`).
2. **Content / fetch** (i18n messages, release JSON, Formspree) — stays in a colocated hook or props; **not** mirrored into Zustand as a second cache.
3. **App chrome** (locale via `I18nProvider`, router) — existing providers; **not** stuffed into each view store.
4. **Truly local ephemeral UI** (one lightbox open used only in one component) — plain `useState` is fine.

### Canonical shape

```text
App ──thin shared deps──► PageRoot
                            ├─ XStoreProvider  (createXStore once per mount)
                            ├─ useXDomain(...)  (reads UI from store)
                            └─ children ──useXUi(selector)──► store
```

### Put in the view Zustand store

| Yes | No |
| --- | --- |
| Tabs, filters, selection shared by siblings | i18n message trees / release payloads as SoT |
| State that should reset on leave | Locale, router, analytics IDs as accidental global singleton |

### Anti-patterns

- Singleton module-level `create(...)` for **page** UI that survives navigation and leaks tabs/filters.
- Replacing every fetch hook with Zustand while still loading JSON from random components (double SoT).
- Passing `value` + `setValue` through page roots after the store exists.

## Consequences

### Positive

- Page wiring shrinks; sections split without prop drilling.
- Same template as Doc Hub / Finance.

### Negative / risks

- Contributors must learn UI-vs-content boundary.
- Gradual migration: do not rewrite every `useState` in one PR.

### Reference implementation (this repo)

- `src/pages/benchmark/benchmarkUiStore.ts` + `BenchmarkStoreContext.tsx` — first pilot (Benchmark tabs).

### Follow-ups

- Apply when splitting heavy pages (Benchmark sections, future docs hub on site, admin panels if promoted).
- Pair with [frontend-code-style.md](../frontend-code-style.md).

## Related

- [frontend-code-style.md](../frontend-code-style.md)
- Doc Hub ADR 0007 (desktop app; same hybrid)