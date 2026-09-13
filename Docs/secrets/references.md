# Database Stores Credential References

ProviderConfig references credentials indirectly (e.g., CredentialRef = "xkiro-main"). Actual secret resolved through secret-store abstraction. Raw provider API keys must not be embedded in normal persisted provider configuration.
