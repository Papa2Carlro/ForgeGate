---
name: research-slice
description: >
  Run a bounded research + characterization slice via doc-memory: pre-flight
  prior Research Findings (RF-*), deepen with Docs/GitNexus/Wire Trace, then
  close out as Verdict A (confirmed bug) or B (legitimate). Use when the user
  asks for research, characterization, Wire Trace correlation, prior finding,
  or research closeout A/B — especially ExistingKnot / C4 / MEC investigations.
managed-by: doc-memory
---

# Research slice

Bounded investigation. **Production diff = 0** until Verdict **A** is confirmed
and a planning task exists. Do **not** create a production task for Verdict **B**.

## Verdict semantics (do not invert)

| Verdict | Meaning | Persist | Planning task |
|---------|---------|---------|---------------|
| **A** | Confirmed bug (stale/incomplete world, parity violation) | `finding_create` | `task_create` after confirm |
| **B** | Legitimate fail-closed / expected reject | `finding_create` (so we do **not** re-dig) | No |

Both A and B **must** write an RF-* finding. B especially — otherwise the next
chat redoes the same Wire Trace archaeology.

## Pre-flight (before code)

1. `doc_use_repo` if the MCP pin is wrong (e.g. Assets vs doc-hub).
2. `doc_session(task)` — board, capsules, confidence, questions, **Research Findings**.
3. `finding_match(query)` and/or `finding_list` on symbol/event
   (`C4StageCorrelation`, `PerformanceAnomaly`, …). Fallback if tools missing:
   `confidence_match` / `question_list` / `capsule_list` + `doc_search`.
4. If an **active B** finding matches and is not obviously stale → **STOP**:
   cite RF id, summary, evidence tests/trace lines; do not re-walk the same L-lines.
5. Search `*CharacterizationTests` (GitNexus / `rg`) before inventing new fixtures.

## Research

Follow the user’s slice brief when given (AGENTS.md → ADR → GitNexus → Wire Trace
→ pipeline). Otherwise:

1. Project `AGENTS.md` / domain skill routers.
2. Doc-memory: ADR / behavior sections (`doc_section` / `doc_map_section`).
3. GitNexus; if index stale, note it and backfill call sites with `rg`.
4. Wire Trace for named lines / event keys.
5. Map the exact pipeline; answer the user’s questions with evidence.

Production = 0. Characterization tests only when A or B evidence criteria in the
user brief are met.

## Closeout

### B — legitimate

```
finding_create(
  title="…",
  verdict="B",
  symbol="ExistingKnotDrag.C4StageCorrelation",  # or event key
  summary="<one paragraph>",
  evidence_json='{"trace_lines":["L…"],"tests":["…CharacterizationTests"],"notes":"…"}',
  path_glob="**/Wire2LegRouteSolver*",  # optional
  domain="wires",
)
```

No `task_create`. Optional `capsule_capture` for long handoff notes.

### A — confirmed bug

Same `finding_create(verdict="A", …)` **plus** `task_create` with canonical owner.
Production fix is a separate follow-up.

### Report shape

- Table of correlation/events: cause, geometry, class A/B
- Symbols / call sites
- Wire Trace evidence
- Test results (if any)
- If A: owner + minimal direction
- `production diff = 0` for research-only slices
- Next zone (open board task or debt)

## Do NOT

- Skip pre-flight `finding_match` / session findings
- Create a planning task for Verdict B
- Re-investigate an active fresh B finding from scratch
- Invert A/B meanings
- Mix production edits into a research-only slice
