using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing;

public sealed class HardRouteEligibilityEvaluator : IRouteEligibilityEvaluator
{
    public RouteEligibilityResult Evaluate(CanonicalChatRequest request, ModelRoute route)
    {
        var reasons = new List<string>();

        if (!route.Enabled)
            reasons.Add("Disabled");

        // Tools capability: only enforce if request implies tool usage.
        // For this slice, we treat any non-empty request with a tool-related signal as requiring Tools.
        // Since CanonicalChatRequest currently has no explicit Tools field, we defer full tool-capability
        // enforcement to a later slice unless the request model is extended.
        // For now: if route has no Tools capability and we detect a future tool signal, filter.
        // Currently: no tool signal in CanonicalChatRequest, so Tools filter is deferred.

        if (reasons.Any())
            return RouteEligibilityResult.Ineligible(reasons);

        return RouteEligibilityResult.Eligible();
    }
}
