# Validation Ownership

Validation belongs to architectural boundary that owns the rule. Conceptual ownership: API boundary → transport/schema/request validation; Protocol Adapter → protocol semantic/normalization validation; Domain/value objects → domain invariants; Application/use case → use-case preconditions; Provider/route boundary → capability/compatibility validation; Startup/config → configuration validation. No universal validation layer.
