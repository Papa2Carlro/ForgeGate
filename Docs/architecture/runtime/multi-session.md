# Multi-Session Concurrency

Baseline requirement: 4+ concurrent agent sessions from start. Not a future optimization.

Routing must account globally for provider limits (max concurrency, RPM, TPM, hourly budgets, cooldowns). Avoid per-session provider logic unaware of global capacity.
