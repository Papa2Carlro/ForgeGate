# Implementation Order

Guidance (not permanent hard dependency graph):

1. Gateway vertical happy-path slice.
2. Provider / LogicalModel / ModelRoute foundations.
3. Provider failure normalization.
4. Router + RoutingDecision.
5. Health + CapacityCoordinator.
6. Streaming.
7. Persistence / Activity.
8. SessionResolver.
9. Tool interception + deterministic policy.
10. Semantic policy escalation.
11. CompletionVerifier.
12. Admin UI.

Task recovery/checkpoint handoff after base gateway and supervision foundations stable.
