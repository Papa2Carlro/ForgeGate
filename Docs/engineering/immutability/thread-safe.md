# Thread-Safe State Owners

Shared-state owners responsible for their own concurrency safety. Implementation details: lock; SemaphoreSlim; ConcurrentDictionary; Channel; Interlocked; other .NET primitives. Primitive is implementation detail. Do NOT expose mutable dictionaries/collections for external mutation. No actor framework in MVP solely for concurrency control.
