# Narrow Explicit Transaction Boundaries

Where several persistence operations must be atomic, use explicit narrow transaction/consistency boundary appropriate to that use case. No generic UnitOfWork solely for pattern compliance. Exact transaction contract created only when real atomic multi-store use case requires it.
