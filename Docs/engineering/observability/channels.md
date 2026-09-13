# Observability Channels

Separate concerns: Technical diagnostics → ILogger / structured logs. Durable business/audit facts → ActivityEvent / persistence. Metrics → counters / histograms / gauges. Distributed/request tracing → OpenTelemetry spans. Do NOT use one channel for all purposes.
