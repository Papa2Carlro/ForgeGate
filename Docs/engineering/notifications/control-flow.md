# Control Flow Must Remain Explicit

If correctness requires an operation, invoke explicitly. Route selection, capacity acquisition, provider execution, required policy enforcement must remain visible in main orchestration path. Do NOT hide mandatory steps behind event handlers.
