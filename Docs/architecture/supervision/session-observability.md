# Session Observability

Internal logs/traces of agent activity, queryable by policy logic, reasoners, supervisors, admin UI, audit.

Dimensions: session, request/turn, provider/model, tool, action, affected paths, policy decision, rejection reason, previous actions/violations, retry/replan relationships.

Persistence/data model NOT decided. Do not lock into a specific DTO/database schema prematurely.
