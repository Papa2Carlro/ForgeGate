# Layered Conservative Session Resolution

OpenAI Chat Completions must NOT be assumed to provide stable conversation ID.

Priority: 1) explicit client session/conversation/correlation identifier; 2) existing ForgeGate client/session binding; 3) stable workspace + conversation signals; 4) bounded continuity/fingerprint heuristic; 5) insufficient confidence → new session.

False merge more dangerous than unnecessary split. Potential confidence concepts: explicit; strong; inferred; new. Schema undecided.
