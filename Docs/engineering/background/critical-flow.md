# Critical Request Flow Is Not Background Work

Do NOT move required synchronous request correctness into background execution. If current request requires action before completion, keep control flow explicit in use case. Background workers for asynchronous maintenance/refresh concerns.
