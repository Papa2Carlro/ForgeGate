namespace ForgeGate.Application.Chat.Routing.Capacity;

/// <summary>
/// Immutable snapshot of route capacity state.
/// Provides read-only visibility into current capacity without exposing mutation primitives.
/// </summary>
public sealed class RouteCapacitySnapshot
{
    /// <summary>
    /// Gets whether the route has a configured concurrency limit.
    /// </summary>
    public bool IsBounded { get; }

    /// <summary>
    /// Gets the maximum concurrent executions limit, or null if unbounded.
    /// </summary>
    public int? MaxConcurrentExecutions { get; }

    /// <summary>
    /// Gets the current number of active executions.
    /// </summary>
    public int ActiveExecutions { get; }

    /// <summary>
    /// Gets the number of available slots, or null if unbounded.
    /// </summary>
    public int? AvailableSlots { get; }

    /// <summary>
    /// Gets whether the route is currently at capacity.
    /// </summary>
    public bool IsAtCapacity { get; }

    internal RouteCapacitySnapshot(bool isBounded, int? maxConcurrentExecutions, int activeExecutions)
    {
        IsBounded = isBounded;
        MaxConcurrentExecutions = maxConcurrentExecutions;
        ActiveExecutions = activeExecutions;

        if (isBounded && maxConcurrentExecutions.HasValue)
        {
            AvailableSlots = Math.Max(0, maxConcurrentExecutions.Value - activeExecutions);
            IsAtCapacity = AvailableSlots.Value == 0;
        }
        else
        {
            AvailableSlots = null;
            IsAtCapacity = false;
        }
    }
}
