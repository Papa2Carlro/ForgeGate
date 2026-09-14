using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Capacity;

/// <summary>
/// In-memory thread-safe capacity coordinator keyed by ModelRouteId.
/// Uses SemaphoreSlim per bounded route for atomic acquisition.
/// Unbounded routes (MaxConcurrentExecutions == null) always acquire successfully.
/// Implements both IRouteCapacityCoordinator and IRouteCapacityStateProvider
/// because they share the same underlying state.
/// </summary>
public sealed class InMemoryRouteCapacityCoordinator : IRouteCapacityCoordinator, IRouteCapacityStateProvider
{
    private readonly object _lock = new();
    private readonly Dictionary<ModelRouteId, SemaphoreSlim> _semaphores = new();
    private readonly Dictionary<ModelRouteId, int> _limits = new();

    /// <summary>
    /// Attempts to acquire capacity for the given route.
    /// </summary>
    public async Task<RouteCapacityAcquireResult> TryAcquireAsync(ModelRoute route, CancellationToken cancellationToken)
    {
        if (route == null)
            throw new ArgumentNullException(nameof(route));

        // Check if route has a configured limit
        int? configuredLimit = route.MaxConcurrentExecutions;

        if (configuredLimit.HasValue && configuredLimit.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(route.MaxConcurrentExecutions), "MaxConcurrentExecutions must be null (unbounded) or >= 1");

        if (!configuredLimit.HasValue)
        {
            // Unbounded route - always acquire
            return RouteCapacityAcquireResult.Acquired(new RouteCapacityReservation(() => { }));
        }

        int limit = configuredLimit.Value;

        // Get or create semaphore with proper locking
        SemaphoreSlim semaphore = GetOrCreateSemaphore(route.ModelRouteId, limit);

        // Try to acquire without blocking
        bool acquired = await semaphore.WaitAsync(0, cancellationToken);
        if (!acquired)
            return RouteCapacityAcquireResult.AtCapacity();

        // Create reservation that releases the semaphore
        var reservation = new RouteCapacityReservation(() => semaphore.Release());
        return RouteCapacityAcquireResult.Acquired(reservation);
    }

    /// <summary>
    /// Gets a read-only snapshot of capacity state for the given route.
    /// </summary>
    public RouteCapacitySnapshot GetSnapshot(ModelRoute route)
    {
        if (route == null)
            throw new ArgumentNullException(nameof(route));

        int? configuredLimit = route.MaxConcurrentExecutions;

        if (!configuredLimit.HasValue || configuredLimit.Value <= 0)
        {
            // Unbounded
            return new RouteCapacitySnapshot(isBounded: false, maxConcurrentExecutions: null, activeExecutions: 0);
        }

        int active;
        lock (_lock)
        {
            if (!_semaphores.TryGetValue(route.ModelRouteId, out var semaphore))
                active = 0;
            else
                active = configuredLimit.Value - semaphore.CurrentCount;
        }

        return new RouteCapacitySnapshot(isBounded: true, maxConcurrentExecutions: configuredLimit, activeExecutions: active);
    }

    private SemaphoreSlim GetOrCreateSemaphore(ModelRouteId routeId, int limit)
    {
        lock (_lock)
        {
            if (_semaphores.TryGetValue(routeId, out var existing))
                return existing;

            var sem = new SemaphoreSlim(limit, limit);
            _semaphores[routeId] = sem;
            _limits[routeId] = limit;
            return sem;
        }
    }

    /// <summary>
    /// Gets current active count for a route (for testing/observability).
    /// </summary>
    public int GetActiveCount(ModelRouteId routeId)
    {
        lock (_lock)
        {
            if (!_semaphores.TryGetValue(routeId, out var semaphore))
                return 0;
            int limit = _limits.TryGetValue(routeId, out int l) ? l : 0;
            return limit - semaphore.CurrentCount;
        }
    }

    /// <summary>
    /// Clears all state (for testing).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            foreach (var sem in _semaphores.Values)
                sem.Dispose();
            _semaphores.Clear();
            _limits.Clear();
        }
    }
}
