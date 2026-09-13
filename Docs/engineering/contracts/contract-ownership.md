# Contract Ownership

Contracts/interfaces belong to consumer/policy-owning side, not implementation side. Examples: Application/Secrets/ISecretStore (Infrastructure/Secrets/EnvironmentSecretStore); Application/Capacity/ICapacityCoordinator (Infrastructure/Capacity/InProcessCapacityCoordinator); Application/Sessions/ISessionStore (Infrastructure/Persistence/EfSessionStore). No global ForgeGate.Abstractions dumping ground. No interface for every class.
