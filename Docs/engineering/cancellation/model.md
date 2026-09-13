# Cancellation Model

Structured end-to-end cancellation. Inbound HTTP/request cancellation enters application use case and passes through async I/O boundaries. Async APIs that may wait/block on I/O should accept CancellationToken where appropriate. Do NOT silently drop cancellation propagation.
