# Domain Dependency Discipline

ForgeGate.Domain remains dependency-light. External packages must not leak into Domain without strong architectural reason. Prefer BCL/native value objects and domain logic. Infrastructure/package concerns at natural boundaries.
