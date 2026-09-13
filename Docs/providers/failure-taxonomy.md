# Normalized Provider Failure Taxonomy

Normalized categories (conceptual, may evolve): AuthenticationFailed; AuthorizationFailed; RateLimited; QuotaExhausted; ConcurrencyLimited; ProviderUnavailable; Timeout; NetworkFailure; ModelUnavailable; InvalidModel; CapabilityUnsupported; ContextExceeded; InvalidRequest; MalformedResponse; StreamInterrupted; UnknownProviderFailure.

Preserve raw upstream status/code/message/evidence. Normalized category + retryability + failure scope + retry-after/reset info when available. Scope may include request, model route, provider, credential, quota window, global service.
