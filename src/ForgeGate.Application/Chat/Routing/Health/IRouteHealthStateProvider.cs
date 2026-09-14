using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Health;

public interface IRouteHealthStateProvider
{
    RouteHealthStatus GetHealth(ModelRouteId routeId);
    void SetHealth(ModelRouteId routeId, RouteHealthStatus status);
}
