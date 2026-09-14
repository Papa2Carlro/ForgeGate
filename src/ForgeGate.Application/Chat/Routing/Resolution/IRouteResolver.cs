using ForgeGate.Application.Chat;

namespace ForgeGate.Application.Chat.Routing.Resolution;

/// <summary>
/// Application-owned route resolution contract.
/// Resolves a canonical chat request to an explicit model route.
/// </summary>
public interface IRouteResolver
{
    /// <summary>
    /// Resolves the canonical chat request to a route resolution outcome.
    /// </summary>
    /// <param name="request">The canonical chat request to resolve.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The route resolution outcome.</returns>
    Task<RouteResolutionOutcome> ResolveAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken);
}