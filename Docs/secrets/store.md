# Secret Store Abstraction

Application/routing logic must not directly depend on environment variables, PostgreSQL secret columns, OS keychain APIs, or Vault APIs.

Concept: SecretStore abstraction. MVP: environment/local-development source. Future possible: encrypted DB store; OS keychain; external Vault/secret manager. Do NOT implement all future stores now.
