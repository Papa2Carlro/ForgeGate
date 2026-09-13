# Central Capacity Coordinator

Single-instance MVP uses one central in-process Capacity Coordinator. Coordinates concurrent sessions globally before upstream provider calls.

Conceptual responsibilities: active request count; concurrency slots; RPM windows; TPM/token budgets; hourly budgets; provider cooldowns; observed quota state. Capacity scoped by Provider / Credential / ModelRoute.

Router asks conceptually: can capacity be granted? Outcomes: Granted / Wait / Unavailable / QuotaExhausted. Exact API/types NOT finalized.
