# Durable Facts vs Derived Projections

Durable structured facts → derived projections/session views/policy context.

Projections (current session summary, recent policy context, provider health view, touched-resource view) may be cached/in-memory/rebuilt. Not canonical facts.

Not full Event Sourcing: append-only activity/audit facts + derived projections, without making the whole product event-sourced.
