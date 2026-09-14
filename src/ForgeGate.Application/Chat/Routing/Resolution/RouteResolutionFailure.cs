using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Resolution;

/// <summary>
/// Application-owned route failure model for deterministic routing slice.
/// Represents failures that can occur during route resolution.
/// </summary>
public sealed record RouteResolutionFailure
{
    private RouteResolutionFailure(RouteResolutionReason reason, string? details = null)
    {
        Reason = reason;
        Details = details;
    }

    /// <summary>
    /// Gets the reason for the route resolution failure.
    /// </>
    public RouteResolutionReason Reason { get; }

    /// <summary>
    /// Gets additional details about the failure (optional).
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Creates a failure for unknown requested model.
    /// </summary>
    /// <param name="requestedModel">The requested model that was not found.</param>
    /// <returns>A route resolution failure for unknown requested model.</returns>
    public static RouteResolutionFailure UnknownRequestedModel(string requestedModel)
    {
        if (string.IsNullOrWhiteSpace(requestedModel))
            throw new ArgumentException("Requested model cannot be null or empty", nameof(requestedModel));

        return new RouteResolutionFailure(
            RouteResolutionReason.UnknownRequestedModel,
            $"Requested model '{requestedModel}' is not configured");
    }

    /// <summary>
    /// Creates a failure for no configured route.
    /// </summary>
    /// <returns>A route resolution failure for no configured route.</returns>
    public static RouteResolutionFailure NoConfiguredRoute()
    {
        return new RouteResolutionFailure(
            RouteResolutionReason.NoConfiguredRoute,
            "No routes are configured");
    }

    public static RouteResolutionFailure NoEligibleRoute(string requestedModel)
    {
        return new RouteResolutionFailure(
            RouteResolutionReason.NoEligibleRoute,
            $"No eligible route for requested model '{requestedModel}'");
    }

    public static RouteResolutionFailure NoCapacityAvailable(string requestedModel)
    {
        return new RouteResolutionFailure(
            RouteResolutionReason.NoCapacityAvailable,
            $"No capacity available for requested model '{requestedModel}'");
    }
}

/// <summary>
/// Reasons for route resolution failures in the deterministic routing slice.
/// </summary>
public enum RouteResolutionReason
{
    /// <summary>
    /// The requested model was not found in the configured routes.
    /// </summary>
    UnknownRequestedModel,

    /// <summary>
    /// No routes are configured in the system.
    /// </summary>
    NoConfiguredRoute,

    /// <summary>
    /// No eligible route after hard/operational eligibility filtering.
    /// </summary>
    NoEligibleRoute,

    /// <summary>
    /// Best quality tier exists but all candidates are at capacity.
    /// </summary>
    NoCapacityAvailable
}