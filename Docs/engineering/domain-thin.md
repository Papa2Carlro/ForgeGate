# Domain Layer: Thin and Strict

Only concepts with genuine independent domain meaning/invariants belong in Domain. Potential examples: LogicalModel; ModelRoute identity/value concepts; Capability requirements; routing profile rules/value concepts; PolicyDecision value concepts; CompletionStatus; WorkspaceId; SessionId. Exact classes during implementation. Do NOT move something to Domain merely because it sounds business-related.
