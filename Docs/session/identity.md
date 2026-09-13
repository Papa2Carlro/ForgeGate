# Session vs Workspace Identity

Preserve existing distinction: Instance → Workspace/Project → Session/Chat/Track.

Workspace groups concurrent tracks. Session = independent conversational/execution stream. Public API remains POST /v1/chat/completions; no workspace/session IDs in paths. Internal resolution only, unless optional client metadata available.
