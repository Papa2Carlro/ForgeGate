# ForgeGate — Architecture Overview

Public open-source project. Not just a reverse proxy.

Long-term: hybrid system — OpenAI-compatible LLM gateway + optional Agent Guard.
Current phase: gateway ONLY. Agent Guard is NOT implemented.

Gateway role: endpoint between LLM client and upstream providers.
Future responsibilities: routing, aliases (fast/smart/coding/reviewer), health, retry/failover, quota, capability awareness, discovery, observability.

Routing and policy enforcement must remain separate.
