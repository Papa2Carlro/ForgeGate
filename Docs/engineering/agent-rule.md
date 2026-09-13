# Agent Implementation Rule

All coding agents must: inspect existing architecture before new abstractions; reuse existing contracts; avoid duplicate competing abstractions; keep dependency direction intact; avoid provider-specific logic in core; avoid god classes/services; avoid static/global mutable state; justify stateful singleton usage; keep changes narrow; add focused tests for changed contract; run direct build/test commands as evidence. If request conflicts with documented architecture, report conflict instead of silently bypassing.
