# Immutability Strategy

IMMUTABLE FACTS / VALUE OBJECTS + ENCAPSULATED MUTABLE RUNTIME STATE.

Facts/decisions representing completed observations should not be casually mutated. Potential immutable concepts: RoutingDecision; ProviderFailure; PolicyDecision; CompletionAssessment; ActivityEvent; SessionId; WorkspaceId; CapabilityRequirement. Mutable runtime state allowed where mutation is actual runtime concern: ProviderRuntimeState; CapacityState; SessionRuntimeProjection; HealthProjection.
