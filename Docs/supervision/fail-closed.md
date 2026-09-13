# High-Risk / Ambiguous Mutation When Supervision Fails

If action mutates protected state; deterministic policy cannot conclusively classify; semantic evaluation required; semantic supervisor unavailable → do NOT silently fail open. Preferred: DENY or ASK_USER (depending on client capabilities and policy). Example: ambiguous Python script that may rewrite production source + semantic reasoner unavailable → do not execute automatically.
