# Future Distributed Capacity

MVP remains single-process/in-process. No Redis or distributed locks. Capacity Coordinator kept behind clean application boundary so distributed implementation can be introduced later without rewriting routing semantics.
