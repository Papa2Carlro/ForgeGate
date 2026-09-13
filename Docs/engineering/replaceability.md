# Replaceability

Important subsystems must be replaceable without rewriting unrelated code. Examples: provider adapter; SecretStore; CapacityCoordinator; persistence; Semantic Policy Reasoner.

Use explicit contracts at meaningful architectural boundaries. Not interface for every class.
