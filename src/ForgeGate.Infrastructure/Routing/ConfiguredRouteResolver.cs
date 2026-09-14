using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Infrastructure.Routing;

/// <summary>
/// Infrastructure-backed deterministic route resolver.
/// Resolves requested model aliases to ModelRoute instances using static configuration.
/// </summary>
public sealed class ConfiguredRouteResolver : IRouteResolver
{
    private readonly RoutingConfiguration _configuration;

    public ConfiguredRouteResolver(IOptions<RoutingConfiguration> configuration)
    {
        _configuration = configuration.Value ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Resolves the canonical chat request to a route resolution outcome.
    /// Performs exact match against configured route aliases.
    /// </summary>
    /// <param name="request">The canonical chat request to resolve.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The route resolution outcome.</returns>
    public async Task<RouteResolutionOutcome> ResolveAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken)
    {
        // Application layer validation
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.RequestedModel))
            return RouteResolutionOutcome.Failure(
                RouteResolutionFailure.UnknownRequestedModel(request.RequestedModel ?? string.Empty));

        // Check if any routes are configured
        if (_configuration.Routes == null || !_configuration.Routes.Any())
            return RouteResolutionOutcome.Failure(RouteResolutionFailure.NoConfiguredRoute());

        // Find exact match for requested model alias
        var matchingRoute = _configuration.Routes
            .FirstOrDefault(r => 
                r.Enabled && 
                string.Equals(r.RequestedModelAlias, request.RequestedModel, StringComparison.OrdinalIgnoreCase));

        if (matchingRoute == null)
            return RouteResolutionOutcome.Failure(
                RouteResolutionFailure.UnknownRequestedModel(request.RequestedModel));

        return RouteResolutionOutcome.Success(matchingRoute.ModelRoute);
    }
}