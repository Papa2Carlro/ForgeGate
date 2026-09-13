# Final MVP High-Level Architecture Status

Sufficiently decided for MVP implementation to begin:

- OpenAI Chat Completions external MVP contract.
- Protocol-neutral canonical internal request/response model.
- Provider / LogicalModel / ModelRoute model.
- Capability-based provider adapters.
- Declared vs observed capabilities vs runtime state.
- Provider-local normalization / quirks boundary.
- Normalized failure taxonomy + raw evidence.
- Layered conservative session resolution.
- Durable structured facts + selective raw retention.
- Secret-store abstraction.
- Operational Admin MVP with evolution toward full Control Plane.
- Hard eligibility → quality tier → operational ranking.
- Composite route health.
- Passive-first health.
- Central in-process Capacity Coordinator.
- Profile-driven wait/degrade routing.
- Manual quality baseline + observed runtime performance.
- Capability mismatch as first-class routing reason.
- Compact RoutingDecision.
- Explicit parameter compatibility policy.
- Hybrid buffered streaming.
- Pre-execution tool policy.
- Selective completion verification.
- Risk-based fail-open/fail-closed supervision behavior.
- Structured task recovery architecture designed but deferred from first slices.
