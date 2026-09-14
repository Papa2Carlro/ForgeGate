using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing;

/// <summary>
/// Application-owned read-only contract for observing route capacity state.
/// Does NOT expose acquisition/mutation capabilities.
/// </summary>
public interface IRouteCapacityStateProvider
{
    /// <summary>
    /// Gets a snapshot of the current capacity state for the given route.
    /// </summary>
    RouteCapacitySnapshot GetSnapshot(ModelRoute route);
}
