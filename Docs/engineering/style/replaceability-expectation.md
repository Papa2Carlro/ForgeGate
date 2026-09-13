# Replaceability Expectation

Important subsystem implementations replaceable primarily through composition/config change + new implementation, not unrelated module edits. Examples: EnvironmentSecretStore → KeychainSecretStore; InProcessCapacityCoordinator → distributed coordinator; provider adapter implementation; EF persistence adapter → alternate persistence where required.
