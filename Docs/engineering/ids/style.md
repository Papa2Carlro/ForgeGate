# ID Implementation Style

Prefer lightweight value objects (readonly record struct) rather than raw Guid/string everywhere. Goals: compile-time type safety; clearer method signatures; prevention of accidental ID mixing; alignment with thin strict Domain/value-object design. Do NOT build excessive factory/interface/helper infrastructure around IDs.
