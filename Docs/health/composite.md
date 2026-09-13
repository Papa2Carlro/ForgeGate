# Composite Route Health

Not boolean. Composite dimensions conceptually: AvailabilityState; CredentialState; CapacityState; QuotaState; RecentFailureState; cooldown info; last observation timestamp. Example: Availability=Reachable, Credential=Valid, Capacity=TemporarilyLimited, Quota=Available, RecentFailure=RateLimited → route not globally dead. Exact enums NOT frozen.
