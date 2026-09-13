# External Nullability Normalization

External/provider DTOs may naturally contain many nullable/optional fields. Normalize them early at their boundary. Canonical/application models should generally be stricter than raw external DTOs. Example: provider raw response (nullable/loose) → ProviderResponseNormalizer → canonical response (validated/normalized/stronger invariants).
