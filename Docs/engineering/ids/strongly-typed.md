# Strongly-Typed Internal IDs

Lightweight strongly-typed IDs for important internal identities. Potential examples: ProviderId; LogicalModelId; ModelRouteId; WorkspaceId; SessionId; RoutingDecisionId; ProviderAttemptId; ActivityEventId. Exact list from implementation. Prefer readonly record struct SessionId(Guid Value) over raw Guid/string everywhere.
