using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing;

/// <summary>
/// Explicit typed outcome of route resolution.
/// Represents either success with a ModelRoute or failure with a RouteResolutionFailure.
/// </summary>
public sealed record RouteResolutionOutcome
{
    private RouteResolutionOutcome(bool isSuccess, ModelRoute? route, RouteResolutionFailure? failureValue)
    {
        IsSuccess = isSuccess;
        Route = route;
        FailureValue = failureValue;
    }

    /// <summary>
    /// Gets whether the resolution was successful.
    /// </>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the resolved ModelRoute if successful.
    /// </summary>
    public ModelRoute? Route { get; }

    /// <summary>
    /// Gets the RouteResolutionFailure if unsuccessful.
    /// </summary>
    public RouteResolutionFailure? FailureValue { get; }

    /// <summary>
    /// Creates a successful route resolution outcome.
    /// </summary>
    /// <param name="route">The resolved model route.</param>
    /// <returns>A successful route resolution outcome.</returns>
    public static RouteResolutionOutcome Success(ModelRoute route)
    {
        if (route == null)
            throw new ArgumentNullException(nameof(route));

        return new RouteResolutionOutcome(true, route, null);
    }

    /// <summary>
    /// Creates a failed route resolution outcome.
    /// </summary>
    /// <param name="failure">The route resolution failure.</param>
    /// <returns>A failed route resolution outcome.</returns>
    public static RouteResolutionOutcome Failure(RouteResolutionFailure failure)
    {
        if (failure == null)
            throw new ArgumentNullException(nameof(failure));

        return new RouteResolutionOutcome(false, null, failure);
    }
}