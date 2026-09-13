# External I/O and Database Transactions

Do NOT hold long-running DB transactions around LLM provider calls, external HTTP calls, user interaction, long waits, capacity waiting. Do NOT attempt distributed ACID between PostgreSQL and external providers. Use appropriate explicit consistency/eventual patterns where needed.
