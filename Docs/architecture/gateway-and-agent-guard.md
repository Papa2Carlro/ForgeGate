# Gateway and Agent Guard

Gateway must NOT depend on Agent Guard.

Conceptual split:
- Gateway: routing, providers, observability, policy/supervision hooks.
- Agent Guard (future, optional): tool execution, capability enforcement, approvals, stronger guarantees.

Leave clean boundaries; do not implement Agent Guard runtime now.
