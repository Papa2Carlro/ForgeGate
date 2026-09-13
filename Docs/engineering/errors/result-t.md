# Do Not Use Result<T> Everywhere

Not mechanically wrap every method in Result<T>. Simple deterministic functions/value operations return ordinary values. Explicit outcome types where multiple expected business states meaningful. Avoid both extremes: exceptions for every expected state; Result<T> boilerplate for every trivial method.
