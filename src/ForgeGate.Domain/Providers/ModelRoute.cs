namespace ForgeGate.Domain.Providers;

public sealed record ModelRoute
{
    public ModelRouteId ModelRouteId { get; init; }
    public ProviderId ProviderId { get; init; }
    public LogicalModelId LogicalModelId { get; init; }
    public string ProviderNativeModelId { get; init; }
    public bool Enabled { get; init; }

    private ModelRoute(ModelRouteId modelRouteId, ProviderId providerId, LogicalModelId logicalModelId, string providerNativeModelId, bool enabled)
    {
        ModelRouteId = modelRouteId;
        ProviderId = providerId;
        LogicalModelId = logicalModelId;
        ProviderNativeModelId = providerNativeModelId;
        Enabled = enabled;
    }

    public static ModelRoute FromIds(ProviderId providerId, LogicalModelId logicalModelId, ModelRouteId modelRouteId)
    {
        return new ModelRoute(modelRouteId, providerId, logicalModelId, string.Empty, true);
    }
}
