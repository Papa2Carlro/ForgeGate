# Model Quality Model

MVP: manual declared quality baseline + observed runtime performance signals. Not full automatic benchmarking/reclassification.

DeclaredQualityTier (manual/admin). ObservedPerformance: latency, failure rate, tool-call validity, policy violation frequency, completion-success signals, other operational observations. Observed performance may affect operational ranking. Must NOT silently move model from preferred/acceptable/fallback tiers in MVP.
