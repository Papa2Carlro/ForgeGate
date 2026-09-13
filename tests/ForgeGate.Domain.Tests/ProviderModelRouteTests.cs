using ForgeGate.Domain.Providers;

namespace ForgeGate.Domain.Tests;

public class ProviderModelRouteTests
{
    [Fact]
    public void ProviderId_From_ValidString_CreatesId()
    {
        var id = ProviderId.From("openai");
        Assert.Equal("openai", id.Value);
    }

    [Fact]
    public void ProviderId_From_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProviderId.From(""));
    }

    [Fact]
    public void LogicalModelId_From_ValidString_CreatesId()
    {
        var id = LogicalModelId.From("gpt-4");
        Assert.Equal("gpt-4", id.Value);
    }

    [Fact]
    public void ModelRouteId_From_ValidString_CreatesId()
    {
        var id = ModelRouteId.From("openai:gpt-4");
        Assert.Equal("openai:gpt-4", id.Value);
    }

    [Fact]
    public void ModelRoute_FromIds_CreatesRoute()
    {
        var providerId = ProviderId.From("openai");
        var logicalModelId = LogicalModelId.From("gpt-4");
        var modelRouteId = ModelRouteId.From("openai:gpt-4");
        var route = ModelRoute.FromIds(providerId, logicalModelId, modelRouteId);

        Assert.Equal(providerId, route.ProviderId);
        Assert.Equal(logicalModelId, route.LogicalModelId);
        Assert.Equal(modelRouteId, route.ModelRouteId);
    }

    [Fact]
    public void ModelRoute_Equality_Works()
    {
        var route1 = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );

        var route2 = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );

        Assert.Equal(route1, route2);
    }
}
