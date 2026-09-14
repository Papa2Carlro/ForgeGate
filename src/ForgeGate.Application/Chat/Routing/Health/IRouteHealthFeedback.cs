using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Health;

public interface IRouteHealthFeedback
{
    void RecordSuccess(ModelRoute route);
    void RecordFailure(ModelRoute route, ProviderFailure failure);
}
