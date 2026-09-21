---
name: doc-memory
description: >
  Query project Docs/ via the doc-memory MCP server without reading full markdown
  into context. Use before any Docs/ research: task routing, canon briefs, section
  slices, code-map § refs. Prefer doc_route/doc_brief/doc_section/doc_map_section
  over Read on large Docs files. Also: Doc Hub planning write (tasks) and
  attachments / audit_prompt_get (ADR 0005).
managed-by: doc-memory
---

# Doc memory (token-efficient Docs lookup)

**Do NOT** read whole `Docs/` canon files into context. Use the **doc-memory** MCP.

## Workflow

1. `doc_starter()` — zoom 0 (or `docs://context-pack-short`)
2. `doc_session(task, domain?)` — preferred start
3. `doc_section(path, heading)` OR `doc_map_section("5.2.A")`
4. `doc_symbols` / `doc_code_bridge` — docs ↔ code
5. `doc_diff(path)` — after doc edits
6. `Read` full file — only when exact wording needed

Fallback: `doc_chain(task)` for docs-only one-shot.

## Planning / CR

- `planning_info` / `task_*` / `epic_*` / `attachment_*` / `board_writers`
- Code review: MCP **`audit_prompt_get`** (not CLI-first)
- Multi-command one board: set `DOC_MEMORY_SESSION`, pause with `capsule_capture`
  — recipe `Docs/guides/multi-command-board.md`

## Research closeout

Bounded research / characterization / Wire Trace correlation slices → skill
**`research-slice`** (`finding_match` / `finding_create` Verdict A|B). Do not leave
Verdict B only in chat.

## Agent pack

SoT: `~/.config/doc-hub/agents/`. Sync: `dm init`, `dm skills update`, `dm agents update`.
Skills install to `~/.cursor/skills/doc-memory/` (+ `research-slice/`) and Claude equivalents.
