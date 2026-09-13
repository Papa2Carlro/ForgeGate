# Decision Triggers (Updated)

BEFORE Chat Completions adapter: finalize minimal supported request fields; OpenAI-compatible validation/error behavior; canonical request mapping.
BEFORE routing: finalize first quality tiers; simple operational ranking inputs; compact RoutingDecision shape.
BEFORE health/capacity: define first composite health states; in-process capacity reservation; rate-limit/cooldown update.
BEFORE provider adapters: finalize minimum provider contract; first capability contracts; provider-local normalization; normalized error mappings.
BEFORE parameter compatibility: classify MVP parameters; safe translations; semantic requirements making route ineligible.
BEFORE policy interception: finalize initial deterministic rules; candidate interception point in streaming pipeline; structural validation boundary; bounded REPLAN flow.
BEFORE semantic Policy Reasoner: review deterministic-policy false positives/unknown cases; then model, prompt/context, escalation threshold, retry limit.
BEFORE automatic quality management: accumulate real runtime observations; then benchmarking/reclassification.
