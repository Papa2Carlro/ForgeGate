using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Resolution;

/// <summary>
/// Context for route resolution with request-local exclusions.
/// Used to support route failover by excluding previously attempted routes.
/// </summary>
public sealed class RouteResolutionContext
{
    /// <summary>
    /// The canonical chat request being resolved.
    /// </summary>
    public CanonicalChatRequest Request { get; }

    /// <summary>
    /// Route IDs that have already been attempted and should be excluded.
    /// This set is request-local and immutable from the resolver's perspective.
    /// </summary>
    public IReadOnlySet<ModelRouteId> ExcludedRouteIds { get; }

    /// <summary>
    /// The quality tier that has been locked by the first successful resolution.
    /// Failover attempts must remain within this tier.
    /// </summary>
    public DeclaredQualityTier? LockedQualityTier { get; }

    internal RouteResolutionContext(
        CanonicalChatRequest request,
        IReadOnlySet<ModelRouteId> excludedRouteIds,
        DeclaredQualityTier? lockedQualityTier)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        ExcludedRouteIds = excludedRouteIds ?? throw new ArgumentNullException(nameof(excludedRouteIds));
        LockedQualityTier = lockedQualityTier;
    }

    /// <summary>
    /// Creates a fresh resolution context for the initial route selection.
    /// </summary>
    public static RouteResolutionContext CreateFresh(CanonicalChatRequest request)
    {
        return new RouteResolutionContext(request, new HashSet<ModelRouteId>().ToHashSet(), null);
    }

    /// <summary>
    /// Creates a new context with the specified route excluded and quality tier locked.
    /// </summary>
    public RouteResolutionContext WithExclusionAndLockedTier(ModelRouteId excludedRouteId, DeclaredQualityTier lockedTier)
    {
        var exclusions = new HashSet<ModelRouteId>(ExcludedRouteIds)
        {
            excludedRouteId
        };
        return new RouteResolutionContext(Request, exclusions, lockedTier);
    }
}
