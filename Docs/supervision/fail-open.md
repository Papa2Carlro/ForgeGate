# Safe / Read-Only Action When Semantic Supervision Fails

If action is low-risk/read-only; deterministic policy does not reject; semantic supervisor unavailable/times out → ALLOW + audit/supervision warning. Examples: ordinary file reads; status inspection; non-mutating queries. Exact low-risk classification = implementation/config work.
