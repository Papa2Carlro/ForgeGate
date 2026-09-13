# Transactional Response Boundary

Distinguish pre-commit and committed response state.

Before commit: transparent retry/failover allowed.
After commit: current stream/turn is not silently replaced by another model.

Exact streaming commit rules NOT decided.
