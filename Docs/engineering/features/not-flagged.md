# What Should Not Be Feature-Flagged

Not for basic stable concerns: standard validation; canonical request mapping; strongly typed IDs; normal logging; core domain invariants; ordinary mandatory security behavior. Do NOT use flags to compensate for poor modularity.
