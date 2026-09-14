using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Resolution;
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
    private readonly IRouteHealthStateProvider? _healthStateProvider;

    public ConfiguredRouteResolver(
        IOptions<RoutingConfiguration> configuration,
        IRouteEligibilityEvaluator eligibilityEvaluator,
        IRouteHealthRanker healthRanker,
        IRouteCapacityStateProvider capacityStateProvider,
        IRouteHealthStateProvider? healthStateProvider = null)
    {
        _configuration = configuration.Value ?? throw new ArgumentNullException(nameof(configuration));
        _eligibilityEvaluator = eligibilityEvaluator ?? throw new ArgumentNullException(nameof(eligibilityEvaluator));
        _healthRanker = healthRanker ?? throw new ArgumentNullException(nameof(healthRanker));
        _capacityStateProvider = capacityStateProvider ?? throw new ArgumentNullException(nameof(capacityStateProvider));
        _healthStateProvider = healthStateProvider;
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

        // Select BEST AVAILABLE DECLARED QUALITY TIER only - no fallback
        var bestTier = eligible
            .Select(e => e.candidate.QualityTier)
            .OrderBy(t => t == DeclaredQualityTier.Preferred ? 0 : t == DeclaredQualityTier.Acceptable ? 1 : 2)
            .First();

        var bestTierRoutes = eligible
            .Where(e => e.candidate.QualityTier == bestTier)
            .Select(e => e.effectiveRoute)
            .ToList();

        // Order routes within the best quality tier by health (Healthy > Unknown > Degraded)
        var healthOrderedRoutes = _healthRanker.OrderByHealth(bestTierRoutes);

        // Filter out routes that are at capacity (only bounded routes can be at capacity)
        var availableRoutes = new List<ModelRoute>();
        foreach (var route in healthOrderedRoutes)
        {
            var snapshot = _capacityStateProvider.GetSnapshot(route);
            if (!snapshot.IsAtCapacity)
                availableRoutes.Add(route);
        }

        if (!availableRoutes.Any())
            return RouteResolutionOutcome.Failure(
                RouteResolutionFailure.NoCapacityAvailable(request.RequestedModel));

        // Sort by: health (primary) → capacity headroom (secondary within same health)
        // Group by health status using _healthStateProvider if available
        var healthy = new List<ModelRoute>();
        var unknown = new List<ModelRoute>();
        var degraded = new List<ModelRoute>();

        foreach (var route in availableRoutes)
        {
            var health = _healthStateProvider?.GetHealth(route.ModelRouteId) ?? RouteHealthStatus.Unknown;
            switch (health)
            {
                case RouteHealthStatus.Healthy:
                    healthy.Add(route);
                    break;
                case RouteHealthStatus.Degraded:
                    degraded.Add(route);
                    break;
                default:
                    unknown.Add(route);
                    break;
            }
        }

        // Sort within each health group by capacity headroom (more slots first)
        healthy.Sort((a, b) =>
            (_capacityStateProvider.GetSnapshot(b).AvailableSlots ?? 0)
            .CompareTo(_capacityStateProvider.GetSnapshot(a).AvailableSlots ?? 0));

        unknown.Sort((a, b) =>
            (_capacityStateProvider.GetSnapshot(b).AvailableSlots ?? 0)
            .CompareTo(_capacityStateProvider.GetSnapshot(a).AvailableSlots ?? 0));

        degraded.Sort((a, b) =>
            (_capacityStateProvider.GetSnapshot(b).AvailableSlots ?? 0)
            .CompareTo(_capacityStateProvider.GetSnapshot(a).AvailableSlots ?? 0));

        var orderedWithCapacity = new List<ModelRoute>(healthy);
        orderedWithCapacity.AddRange(unknown);
        orderedWithCapacity.AddRange(degraded);

        var selectedRoute = orderedWithCapacity.First();

        return RouteResolutionOutcome.Success(selectedRoute);
    }
}
