using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing;

public sealed class RouteOperationalEligibilityEvaluator : IRouteEligibilityEvaluator
{
    private readonly IRouteEligibilityEvaluator _hardEvaluator;
    private readonly IRouteHealthStateProvider _healthProvider;

    public RouteOperationalEligibilityEvaluator(IRouteEligibilityEvaluator hardEvaluator, IRouteHealthStateProvider healthProvider)
    {
        _hardEvaluator = hardEvaluator ?? throw new ArgumentNullException(nameof(hardEvaluator));
        _healthProvider = healthProvider ?? throw new ArgumentNullException(nameof(healthProvider));
    }

    public RouteEligibilityResult Evaluate(CanonicalChatRequest request, ModelRoute route)
    {
        var hardResult = _hardEvaluator.Evaluate(request, route);
        if (!hardResult.IsEligible)
            return hardResult;

        var health = _healthProvider.GetHealth(route.ModelRouteId);
        if (health == RouteHealthStatus.Unavailable)
            return RouteEligibilityResult.Ineligible("RouteUnavailable");

        return RouteEligibilityResult.Eligible();
    }
}
