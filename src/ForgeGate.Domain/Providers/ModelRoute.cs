namespace ForgeGate.Domain.Providers;

public sealed record ModelRoute
{
    public ModelRouteId ModelRouteId { get; init; }
    public ProviderId ProviderId { get; init; }
    public LogicalModelId LogicalModelId { get; init; }
    public string ProviderNativeModelId { get; init; }
    public bool Enabled { get; init; }
    public ModelCapability Capabilities { get; init; }
    public DeclaredQualityTier QualityTier { get; init; }
    public int? MaxConcurrentExecutions { get; init; }

    private ModelRoute(ModelRouteId modelRouteId, ProviderId providerId, LogicalModelId logicalModelId, string providerNativeModelId, bool enabled, ModelCapability capabilities, DeclaredQualityTier qualityTier, int? maxConcurrentExecutions = null)
    {
        ModelRouteId = modelRouteId;
        ProviderId = providerId;
        LogicalModelId = logicalModelId;
        ProviderNativeModelId = providerNativeModelId;
        Enabled = enabled;
        Capabilities = capabilities;
        QualityTier = qualityTier;
        MaxConcurrentExecutions = maxConcurrentExecutions;
    }

    public static ModelRoute FromIds(ProviderId providerId, LogicalModelId logicalModelId, ModelRouteId modelRouteId)
    {
        return new ModelRoute(modelRouteId, providerId, logicalModelId, string.Empty, true, ModelCapability.None, DeclaredQualityTier.Acceptable);
    }

    public static ModelRoute FromIdsWithOptions(ProviderId providerId, LogicalModelId logicalModelId, ModelRouteId modelRouteId, string providerNativeModelId, bool enabled, ModelCapability capabilities, DeclaredQualityTier qualityTier = DeclaredQualityTier.Acceptable, int? maxConcurrentExecutions = null)
    {
        return new ModelRoute(modelRouteId, providerId, logicalModelId, providerNativeModelId, enabled, capabilities, qualityTier, maxConcurrentExecutions);
    }
}
