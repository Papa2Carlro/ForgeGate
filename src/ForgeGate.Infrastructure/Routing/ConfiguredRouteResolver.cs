using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Infrastructure.Routing;

public sealed class ConfiguredRouteResolver : IRouteResolver
{
    private readonly RoutingConfiguration _configuration;
    private readonly IRouteEligibilityEvaluator _eligibilityEvaluator;

    public ConfiguredRouteResolver(IOptions<RoutingConfiguration> configuration, IRouteEligibilityEvaluator eligibilityEvaluator)
    {
        _configuration = configuration.Value ?? throw new ArgumentNullException(nameof(configuration));
        _eligibilityEvaluator = eligibilityEvaluator ?? throw new ArgumentNullException(nameof(eligibilityEvaluator));
    }

    public async Task<RouteResolutionOutcome> ResolveAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.RequestedModel))
            return RouteResolutionOutcome.Failure(
                RouteResolutionFailure.UnknownRequestedModel(request.RequestedModel ?? string.Empty));

        if (_configuration.Routes == null || !_configuration.Routes.Any())
            return RouteResolutionOutcome.Failure(RouteResolutionFailure.NoConfiguredRoute());

        var candidates = _configuration.Routes
            .Where(r => string.Equals(r.RequestedModelAlias, request.RequestedModel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!candidates.Any())
            return RouteResolutionOutcome.Failure(
                RouteResolutionFailure.UnknownRequestedModel(request.RequestedModel));

        var eligible = new List<ModelRoute>();
        foreach (var candidate in candidates)
        {
            var result = _eligibilityEvaluator.Evaluate(request, candidate.ModelRoute);
            if (result.IsEligible)
                eligible.Add(candidate.ModelRoute);
        }

        if (!eligible.Any())
            return RouteResolutionOutcome.Failure(
                RouteResolutionFailure.NoEligibleRoute(request.RequestedModel));

        // Best available tier selection: Preferred > Acceptable > Fallback
        var eligibleCandidates = candidates
            .Where(c => eligible.Contains(c.ModelRoute))
            .ToList();

        var bestTier = eligibleCandidates
            .Select(c => c.QualityTier)
            .OrderBy(t => t == DeclaredQualityTier.Preferred ? 0 : t == DeclaredQualityTier.Acceptable ? 1 : 2)
            .First();

        var selectedRoute = eligibleCandidates
            .Where(c => c.QualityTier == bestTier)
            .Select(c => c.ModelRoute)
            .First();

        return RouteResolutionOutcome.Success(selectedRoute);
    }
}
