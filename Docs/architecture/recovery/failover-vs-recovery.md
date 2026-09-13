# Failover vs Recovery

Two independent mechanisms:

1. Request/turn-level failover (pre-commit): retry, switch provider/route, same logical model through another provider. Client may be unaware.
2. Task/execution-level recovery (post-commit): after output/tool-call committed, do NOT splice another provider/model into same stream. Continue through new execution turn with structured recovery context.
