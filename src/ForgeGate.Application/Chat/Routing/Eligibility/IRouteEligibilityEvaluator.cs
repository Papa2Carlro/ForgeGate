using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Eligibility;

public interface IRouteEligibilityEvaluator
{
    RouteEligibilityResult Evaluate(CanonicalChatRequest request, ModelRoute route);
}
