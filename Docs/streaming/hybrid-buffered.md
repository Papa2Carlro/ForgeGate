# Hybrid Buffered Streaming

HYBRID_BUFFERED_STREAMING = DECIDED.

Not pure passthrough; not fully buffered before sending.

Upstream → pre-commit buffer/validation → commit boundary → client stream.

Before commit: validate structure, inspect tool-call fragments, policy checks, transparent retry/failover, reject malformed output.
After commit: no silent splice of different model/provider; use task-level recovery / structured handoff.

Tool calls need special treatment: partial JSON/arguments must not be exposed as executable before validated.

Still undecided: exact text commit threshold; exact tool-call commit rule; buffer size; timeout behavior while pre-commit; client-specific streaming quirks.
