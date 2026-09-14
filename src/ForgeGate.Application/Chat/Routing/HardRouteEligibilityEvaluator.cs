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

        if ((request.ToolRequirement == ToolRequirement.Optional || request.ToolRequirement == ToolRequirement.Required)
            && !route.Capabilities.HasFlag(ModelCapability.Tools))
            reasons.Add("CapabilityUnsupported");

        if (reasons.Any())
            return RouteEligibilityResult.Ineligible(reasons);

        return RouteEligibilityResult.Eligible();
    }
}
