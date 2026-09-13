# Shared State Ownership

Every shared mutable runtime concern must have one explicit owner. Conceptual owners: InProcessCapacityCoordinator (capacity/quota/concurrency); ProviderHealthRegistry (provider/model-route health); SessionRuntimeRegistry (active session runtime). Other services must not mutate owner's internal collections directly.
