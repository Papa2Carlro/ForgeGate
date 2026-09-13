# Domain Invariants

Important value/domain invariants must remain inside value/domain concept. Example: invalid SessionId or invalid domain value should not require every caller to remember external validation service. Factories/constructors may enforce invariants. Do NOT force transport/provider validation into Domain.
