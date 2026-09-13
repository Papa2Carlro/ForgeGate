# Internal Session Identity

OpenAI Chat Completions does not guarantee a stable conversation ID. ForgeGate needs a Session Resolver using available signals: correlation metadata, conversation/request metadata, message-history fingerprint, workspace paths from tool context, recent request continuity, other stable client signals.

Exact fingerprint algorithm NOT decided. Session identity and workspace identity are separate; same workspace with different sessions must not collapse.
