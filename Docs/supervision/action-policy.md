# Action Policy

Candidate tool/action calls subject to strict pre-execution policy. Existing layered pipeline: structural validation → deterministic policy → risk/suspicion evaluation → semantic reasoner only when necessary → ALLOW / DENY / REPLAN / ASK_USER. Must evaluate before committed to client for execution. Post-execution logging insufficient.
