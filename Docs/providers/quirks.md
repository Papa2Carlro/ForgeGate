# Provider-Specific Quirks

Provider-specific compatibility behavior localized at provider adapter/normalization boundary. No `if provider == xKiro` through routing/core/application logic.

Pipeline: canonical request → ProviderRequestNormalizer → provider transport → ProviderResponseNormalizer → canonical response.
