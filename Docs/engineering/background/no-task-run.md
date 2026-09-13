# No Task.Run Architecture

No unowned fire-and-forget Task.Run calls as background execution architecture. Background work must have: explicit ownership; lifecycle; cancellation; observability; failure handling.
