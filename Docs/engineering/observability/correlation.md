# Correlation Context

Observability channels share correlation identifiers where available. Potential examples: RequestId; SessionId; WorkspaceId; ProviderAttemptId; RoutingDecisionId. Allows logs, traces, activity facts, metrics to be correlated. Secrets/sensitive payloads must not leak through correlation/observability.
