# Routing Decision as First-Class Fact

Every route selection produces compact RoutingDecision. Do NOT persist full verbose candidate graph for every normal request by default.

Compact RoutingDecision conceptually includes: requested target/profile; selected route; routing action; primary reason; quality tier change; wait/degradation outcome. Exact schema NOT finalized.
