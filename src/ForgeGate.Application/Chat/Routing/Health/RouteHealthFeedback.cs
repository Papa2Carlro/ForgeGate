using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Health;

public sealed class RouteHealthFeedback : IRouteHealthFeedback
{
    private readonly IRouteHealthStateProvider _provider;

    public RouteHealthFeedback(IRouteHealthStateProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public void RecordSuccess(ModelRoute route)
    {
        if (route == null) throw new ArgumentNullException(nameof(route));
        _provider.SetHealth(route.ModelRouteId, RouteHealthStatus.Healthy);
    }

    public void RecordFailure(ModelRoute route, ProviderFailure failure)
    {
        if (route == null) throw new ArgumentNullException(nameof(route));
        if (failure == null) throw new ArgumentNullException(nameof(failure));

        var degradedCategories = new[]
        {
            ProviderFailureCategory.NetworkFailure,
            ProviderFailureCategory.ProviderUnavailable,
            ProviderFailureCategory.MalformedResponse,
            ProviderFailureCategory.Timeout,
            ProviderFailureCategory.UnknownProviderFailure
        };

        if (degradedCategories.Contains(failure.Category))
            _provider.SetHealth(route.ModelRouteId, RouteHealthStatus.Degraded);
        // Auth, rate limit, quota, invalid request, context exceeded, invalid model,
        // capability unsupported, unknown provider failure: health unchanged.
    }
}
