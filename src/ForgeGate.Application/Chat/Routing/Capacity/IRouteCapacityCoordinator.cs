using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Capacity;

/// <summary>
/// Application-owned contract for coordinating concurrent execution capacity per route.
/// </summary>
public interface IRouteCapacityCoordinator
{
    /// <summary>
    /// Attempts to acquire a capacity reservation for the given route.
    /// </summary>
    Task<RouteCapacityAcquireResult> TryAcquireAsync(ModelRoute route, CancellationToken cancellationToken);
}
