using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing;

/// <summary>
/// In-memory thread-safe capacity coordinator keyed by ModelRouteId.
/// Uses SemaphoreSlim per bounded route for atomic acquisition.
/// Unbounded routes (MaxConcurrentExecutions == null) always acquire successfully.
/// </summary>
public sealed class InMemoryRouteCapacityCoordinator : IRouteCapacityCoordinator
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

        if (configuredLimit == null || configuredLimit.Value <= 0)
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
