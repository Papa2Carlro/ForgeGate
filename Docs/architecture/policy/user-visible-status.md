# User-Visible Orchestration Status

Material routing actions must be visible: rate-limited, retrying, switching provider, degrading model, asking user.

If client exposes ask-user/clarification tool, emit native tool call. Otherwise fall back to textual/structured status. Do not assume all clients have native UI controls.
