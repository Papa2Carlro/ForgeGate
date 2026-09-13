# Secret Safety Requirements

Secrets must never appear in: activity logs; audit events; raw provider error storage; telemetry; config exports; admin API responses. Raw upstream errors require redaction before durable logging. Admin UI displays secrets masked/opaque. Public repo contains only safe examples (`.env.example`), never real credentials. Rotation lifecycle deferred.
