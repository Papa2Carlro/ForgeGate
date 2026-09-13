# Policy Enforcement

Layered design:

1. Deterministic policy — fast, unambiguous (force push deny, destructive cleanup deny).
2. Semantic policy reasoner — evaluates intent and context (e.g., native edit vs script rewrite; bulk rename allowed if proportionate).

Policy operates on normalized ActionIntent (kind, target, mechanism, scope), not only raw command strings. Schema not finalized.

Sandbox (/tmp) is not trusted; moving content into protected workspace is a protected mutation.
