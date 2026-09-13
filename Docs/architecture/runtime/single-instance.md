# Single-Instance Concurrent Runtime

One server/process instance supports multiple projects/workspaces and multiple chat/agent sessions concurrently.

Ephemeral coordination (active requests, provider slots, cooldowns, session runtime, observations) is in-process behind clean abstractions.
Durable facts (audit events, config, policies, provider/model definitions, usage history) belong in PostgreSQL.
No IPC, distributed workers, Redis, or clustering yet.
Concrete ConcurrentDictionary/Semaphore details must not leak into core logic.
