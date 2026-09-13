# Persistence Ports Are Intent-Oriented

Contracts expose operations meaningful to consumer. Prefer feature-oriented ports (IActivityStore, ISessionStore, IRoutingConfigStore, IProviderConfigStore) with semantic methods. Avoid generic repository abstraction. Implementation details (EF, PostgreSQL) stay in Infrastructure.
