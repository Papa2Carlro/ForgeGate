# Exceptions

Exceptions for truly unexpected technical failures: programming invariants unexpectedly broken; unexpected serializer/internal failure; infrastructure library failure before normalization. Provider-specific errors normalized at boundary (HttpRequestException → ProviderFailure). Higher-level routing operates on normalized failure concepts.
