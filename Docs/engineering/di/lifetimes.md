# Service Lifetimes

Not singleton by default. Singleton: process-wide stateless/thread-safe services; intentional process-wide coordinators with protected state; caches. Scoped: request/unit-of-work services; EF Core DbContext; request execution context. Transient: lightweight stateless operations. Every stateful singleton must be thread-safe. Singleton must never depend directly on scoped dependency. Lifetime selected per component during implementation.
