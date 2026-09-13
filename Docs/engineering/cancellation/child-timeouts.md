# Child Operation Timeouts

Child operation may use linked CancellationToken with bounded timeout when owning boundary requires. Example: request cancellation + provider timeout. Reason for cancellation/timeout must remain distinguishable where needed.
