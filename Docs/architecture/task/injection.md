# Adaptive Prompt / Context Injection

Allowed to modify upstream context. Must NOT inject one large global system prompt blindly.

Adaptive injection purpose: prevent bad behavior, reinforce relevant task/policy context.

Potential injected info: current goal/scope/constraints; workspace coordination; protected foreign changes; preferred tools; model behavioral guidance; recent policy correction; task-manager state; cross-session activity.
