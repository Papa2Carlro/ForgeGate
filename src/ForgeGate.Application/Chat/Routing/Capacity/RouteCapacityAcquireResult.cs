namespace ForgeGate.Application.Chat.Routing.Capacity;

/// <summary>
/// Result of attempting to acquire route capacity.
/// </summary>
public sealed class RouteCapacityAcquireResult
{
    private RouteCapacityAcquireResult(RouteCapacityAcquireStatus status, RouteCapacityReservation? reservation)
    {
        Status = status;
        Reservation = reservation;
    }

    public RouteCapacityAcquireStatus Status { get; }
    public RouteCapacityReservation? Reservation { get; }

    public static RouteCapacityAcquireResult Acquired(RouteCapacityReservation reservation) =>
        new(RouteCapacityAcquireStatus.Acquired, reservation);

    public static RouteCapacityAcquireResult AtCapacity() =>
        new(RouteCapacityAcquireStatus.AtCapacity, null);
}

/// <summary>
/// Status of a capacity acquisition attempt.
/// </summary>
public enum RouteCapacityAcquireStatus
{
    /// <summary>
    /// Capacity was acquired successfully.
    /// </summary>
    Acquired,

    /// <summary>
    /// Route is at maximum concurrent executions.
    /// </summary>
    AtCapacity
}
