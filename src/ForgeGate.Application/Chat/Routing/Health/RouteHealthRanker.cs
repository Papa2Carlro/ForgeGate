using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Health;

/// <summary>
/// Application-owned health ranker that orders routes by health within a quality tier.
/// Uses IRouteHealthStateProvider to get current health status for each route.
/// </summary>
public sealed class RouteHealthRanker : IRouteHealthRanker
{
    private readonly IRouteHealthStateProvider _healthProvider;

    public RouteHealthRanker(IRouteHealthStateProvider healthProvider)
    {
        _healthProvider = healthProvider ?? throw new ArgumentNullException(nameof(healthProvider));
    }

    /// <summary>
    /// Orders eligible routes by health (Healthy > Unknown > Degraded),
    /// preserving configuration order as a final tie-break.
    /// </summary>
    /// <param name="routes">Eligible routes to order (same quality tier).</param>
    /// <returns>Routes ordered by health then configuration order.</returns>
    public IReadOnlyList<ModelRoute> OrderByHealth(IReadOnlyList<ModelRoute> routes)
    {
        if (routes == null)
            return System.Array.Empty<ModelRoute>();

        if (routes.Count == 0)
            return routes;

        // Group routes by health status while preserving original order within each group
        var healthy = new List<ModelRoute>();
        var unknown = new List<ModelRoute>();
        var degraded = new List<ModelRoute>();

        foreach (var route in routes)
        {
            var health = _healthProvider.GetHealth(route.ModelRouteId);
            switch (health)
            {
                case RouteHealthStatus.Healthy:
                    healthy.Add(route);
                    break;
                case RouteHealthStatus.Unknown:
                    unknown.Add(route);
                    break;
                case RouteHealthStatus.Degraded:
                    degraded.Add(route);
                    break;
                // Unavailable should never reach here due to operational eligibility filtering
            }
        }

        // Combine in order: Healthy > Unknown > Degraded
        var ordered = new List<ModelRoute>();
        ordered.AddRange(healthy);
        ordered.AddRange(unknown);
        ordered.AddRange(degraded);

        return ordered;
    }

    private static ArgumentNullException throwException(string paramName)
    {
        return new ArgumentNullException(paramName);
    }
}