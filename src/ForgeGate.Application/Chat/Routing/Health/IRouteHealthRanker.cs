using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Health;

/// <summary>
/// Application-owned contract for ordering routes by health within a single quality tier.
/// Health ordering is: Healthy &gt; Unknown &gt; Degraded.
/// Unavailable is excluded before ranking by operational eligibility.
/// </summary>
public interface IRouteHealthRanker
{
    /// <summary>
    /// Orders eligible routes by health (Healthy &gt; Unknown &gt; Degraded),
    /// preserving configuration order as a final tie-break.
    /// </summary>
    /// <param name="routes">Eligible routes to order (same quality tier).</param>
    /// <returns>Routes ordered by health then configuration order.</returns>
    IReadOnlyList<ModelRoute> OrderByHealth(IReadOnlyList<ModelRoute> routes);
}