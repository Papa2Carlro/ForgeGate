namespace ForgeGate.Domain.Providers;

public sealed record ModelRoute
{
    public ModelRouteId ModelRouteId { get; init; }
    public ProviderId ProviderId { get; init; }
    public LogicalModelId LogicalModelId { get; init; }
    public string ProviderNativeModelId { get; init; }
    public bool Enabled { get; init; }
    public ModelCapability Capabilities { get; init; }

    private ModelRoute(ModelRouteId modelRouteId, ProviderId providerId, LogicalModelId logicalModelId, string providerNativeModelId, bool enabled, ModelCapability capabilities)
    {
        ModelRouteId = modelRouteId;
        ProviderId = providerId;
        LogicalModelId = logicalModelId;
        ProviderNativeModelId = providerNativeModelId;
        Enabled = enabled;
        Capabilities = capabilities;
    }

    public static ModelRoute FromIds(ProviderId providerId, LogicalModelId logicalModelId, ModelRouteId modelRouteId)
    {
        return new ModelRoute(modelRouteId, providerId, logicalModelId, string.Empty, true, ModelCapability.None);
    }

    public static ModelRoute FromIdsWithOptions(ProviderId providerId, LogicalModelId logicalModelId, ModelRouteId modelRouteId, string providerNativeModelId, bool enabled, ModelCapability capabilities)
    {
        return new ModelRoute(modelRouteId, providerId, logicalModelId, providerNativeModelId, enabled, capabilities);
    }
}
