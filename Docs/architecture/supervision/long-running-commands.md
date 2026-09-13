# Long-Running Command Semantics

Distinguish: finite, long-running, interactive, unknown.

Long-running (e.g., npm run dev) must not be treated as failure due to non-exit. Avoid repeated restarts; use existing process state/output. Exact tracking not decided.
