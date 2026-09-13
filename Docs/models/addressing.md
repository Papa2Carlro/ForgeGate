# Model Addressing Modes

Confirm existing three-level architecture:

1. Virtual profile: forgegate/coding → selects LogicalModel + ModelRoute.
2. Logical model: model/minimax-m2.7 → fixed logical model, selects appropriate ModelRoute.
3. Direct: direct/xkiro/minimax-m2.7 → exact ModelRoute fixed.

Direct mode bypasses model/provider selection only. Does NOT bypass policy, activity logging, supervision, secret handling, compatibility checks.
