# Routing Strategy

Hard eligibility filters → quality tier → simple operational ranking. Not one opaque universal numeric score.

Eligibility: required capabilities, sufficient context, valid credentials, acceptable health, no hard quota exhaustion, task/profile quality floor.

Quality tier: preferred / acceptable / fallback.

Operational ranking inside acceptable tier: available concurrency, recent latency, temporary rate-limit state, quota headroom, recent failure penalty. Exact formula NOT frozen.
