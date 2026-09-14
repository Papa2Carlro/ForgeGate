using System.Collections.Concurrent;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing;

public sealed class InMemoryRouteHealthStateProvider : IRouteHealthStateProvider
{
    private readonly ConcurrentDictionary<ModelRouteId, RouteHealthStatus> _state = new();

    public RouteHealthStatus GetHealth(ModelRouteId routeId)
    {
        return _state.TryGetValue(routeId, out var status) ? status : RouteHealthStatus.Unknown;
    }

    public void SetHealth(ModelRouteId routeId, RouteHealthStatus status)
    {
        _state[routeId] = status;
    }
}
