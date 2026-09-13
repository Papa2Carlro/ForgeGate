# Application Use-Case Coordinators

Narrow feature-scoped use-case coordinators. Example: ChatExecutionService (orchestration only). Coordinates abstractions: IRouteSelector, ICapacityCoordinator, IProviderExecutor, IPolicyEvaluator, IActivityRecorder. Must NOT directly own provider-specific HTTP behavior, SQL, EF Core details, secret retrieval implementation, provider quirk normalization.
