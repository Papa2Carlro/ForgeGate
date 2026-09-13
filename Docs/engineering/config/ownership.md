# Configuration Ownership

Layered configuration ownership. Conceptually: appsettings/environment → bootstrap/infrastructure startup config; PostgreSQL → runtime-editable operational config; SecretStore → API keys/credentials; in-memory state owners → ephemeral runtime state.
