using ForgeGate.Domain.Providers;

namespace ForgeGate.Infrastructure.Routing;

/// <summary>
/// Configuration model for a single route.
/// Represents a configured mapping from a requested model alias to a ModelRoute.
/// </summary>
public sealed class ConfiguredRoute
{
    /// <summary>
    /// Gets or sets the client-facing requested model alias.
    /// This is what clients send in the 'model' field of their requests.
    /// </summary>
    public string RequestedModelAlias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model route to use when the alias matches.
    /// </summary>
    public ModelRoute ModelRoute { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether this route is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the declared quality tier for this route.
    /// </summary>
    public DeclaredQualityTier QualityTier { get; set; } = DeclaredQualityTier.Acceptable;

    /// <summary>
    /// Gets or sets the maximum concurrent executions for this route.
    /// null means unbounded; >= 1 is the maximum active provider executions.
    /// </summary>
    public int? MaxConcurrentExecutions { get; set; }
}

/// <summary>
/// Configuration collection for routes.
/// Represents the configured route mappings for the deterministic resolver.
/// </summary>
public sealed class RoutingConfiguration
{
    /// <summary>
    /// Gets or sets the configured routes.
    /// </summary>
    public IReadOnlyList<ConfiguredRoute> Routes { get; set; } = new List<ConfiguredRoute>();
}