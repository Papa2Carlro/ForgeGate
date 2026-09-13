# Architecture Tests

Architecture/dependency-direction tests may be added when useful. Potential examples: Domain must not reference Infrastructure; Application must not reference provider-specific implementations; API DTOs must not leak into Domain. Do NOT build large custom architecture-testing framework unless real complexity justifies it.
