# Concurrency

Multi-session and concurrent by design. Any process-wide mutable service must be explicitly thread-safe. No accidental mutable global collections. Concurrency semantics required for CapacityCoordinator, health state, active sessions, provider runtime state, workspace coordination. Thread safety is part of contract, not afterthought.
