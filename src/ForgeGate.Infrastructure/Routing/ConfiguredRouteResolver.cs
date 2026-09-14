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
    private readonly IRouteHealthRanker _healthRanker;
    private readonly IRouteCapacityStateProvider _capacityStateProvider;

    public ConfiguredRouteResolver(
        IOptions<RoutingConfiguration> configuration,
        IRouteEligibilityEvaluator eligibilityEvaluator,
        IRouteHealthRanker healthRanker,
        IRouteCapacityStateProvider capacityStateProvider)
    {
        _configuration = configuration.Value ?? throw new ArgumentNullException(nameof(configuration));
        _eligibilityEvaluator = eligibilityEvaluator ?? throw new ArgumentNullException(nameof(eligibilityEvaluator));
        _healthRanker = healthRanker ?? throw new ArgumentNullException(nameof(healthRanker));
        _capacityStateProvider = capacityStateProvider ?? throw new ArgumentNullException(nameof(capacityStateProvider));
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

        var eligible = new List<(ConfiguredRoute candidate, ModelRoute effectiveRoute)>();
        foreach (var candidate in candidates)
        {
            // Merge ConfiguredRoute.MaxConcurrentExecutions into ModelRoute if set
            if (candidate.MaxConcurrentExecutions.HasValue && candidate.MaxConcurrentExecutions.Value <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(ConfiguredRoute.MaxConcurrentExecutions),
                    $"MaxConcurrentExecutions must be null (unbounded) or >= 1, got {candidate.MaxConcurrentExecutions.Value}");

            var effectiveRoute = candidate.MaxConcurrentExecutions.HasValue
                ? candidate.ModelRoute with { MaxConcurrentExecutions = candidate.MaxConcurrentExecutions }
                : candidate.ModelRoute;

            var result = _eligibilityEvaluator.Evaluate(request, effectiveRoute);
            if (result.IsEligible)
                eligible.Add((candidate, effectiveRoute));
        }

        if (!eligible.Any())
            return RouteResolutionOutcome.Failure(
                RouteResolutionFailure.NoEligibleRoute(request.RequestedModel));

        // Iterate through quality tiers from best to worst
        var tierOrder = new[] { DeclaredQualityTier.Preferred, DeclaredQualityTier.Acceptable, DeclaredQualityTier.Fallback };
        
        foreach (var tier in tierOrder)
        {
            var tierRoutes = eligible
                .Where(e => e.candidate.QualityTier == tier)
                .Select(e => e.effectiveRoute)
                .ToList();

            if (!tierRoutes.Any())
                continue;

            // Order routes within this tier by health (Healthy > Unknown > Degraded)
            var healthOrderedRoutes = _healthRanker.OrderByHealth(tierRoutes);

            // Filter out routes that are at capacity
            var availableRoutes = new List<ModelRoute>();
            foreach (var route in healthOrderedRoutes)
            {
                var snapshot = _capacityStateProvider.GetSnapshot(route);
                if (!snapshot.IsAtCapacity)
                    availableRoutes.Add(route);
            }

            if (!availableRoutes.Any())
                continue;

            // Order available routes by capacity headroom (more headroom first)
            var orderedWithCapacity = availableRoutes
                .Select(route => (route, snapshot: _capacityStateProvider.GetSnapshot(route)))
                .OrderByDescending(item => item.snapshot.IsBounded ? 0 : 1)
                .ThenByDescending(item => item.snapshot.AvailableSlots ?? int.MaxValue)
                .Select(item => item.route)
                .ToList();

            return RouteResolutionOutcome.Success(orderedWithCapacity.First());
        }

        // No routes available at any tier
        return RouteResolutionOutcome.Failure(
            RouteResolutionFailure.NoCapacityAvailable(request.RequestedModel));
    }
}
