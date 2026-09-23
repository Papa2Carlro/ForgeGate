using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Providers.XKiro;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

namespace ForgeGate.IntegrationTests;

/// <summary>
/// Explicit opt-in live smoke harness for xKiro real external provider execution.
/// Only runs when FORGEGATE_RUN_REAL_PROVIDER_SMOKE=true AND XKiro__ApiKey is set.
/// </summary>
public class XKiroRealProviderSmokeTests
{
    private static bool IsLiveSmokeEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("FORGEGATE_RUN_REAL_PROVIDER_SMOKE"),
            "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasXKiroCredentials()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XKiro__ApiKey"));
    }

    [SkippableFact]
    public async Task XKiroRealProviderSmoke_ExplicitOptIn_ExecutesFullPipeline()
    {
        Skip.IfNot(IsLiveSmokeEnabled(),
            "Live xKiro smoke skipped: set FORGEGATE_RUN_REAL_PROVIDER_SMOKE=true.");
        Skip.IfNot(HasXKiroCredentials(),
            "Live xKiro smoke skipped: XKiro__ApiKey not configured in runtime environment.");

        var endpoint = Environment.GetEnvironmentVariable("XKiro__Endpoint")
            ?? "https://api.xkiro.com/v1/chat/completions";
        var apiKey = Environment.GetEnvironmentVariable("XKiro__ApiKey")!;

        var config = new MemoryConfigurationSource();
        var memoryConfig = new MemoryConfigurationProvider(config);
        memoryConfig.Add("XKiro:Endpoint", endpoint);
        memoryConfig.Add("XKiro:ApiKey", apiKey);
        var configuration = new ConfigurationRoot(new[] { memoryConfig });

        var httpClient = new HttpClient();
        var provider = new XKiroChatCompletionProvider(httpClient, configuration);

        var route = ModelRoute.FromIds(
            ProviderId.From("xkiro"),
            LogicalModelId.From("minimax-m2.7"),
            ModelRouteId.From("xkiro:minimax-m2.7"));

        var request = new CanonicalChatRequest
        {
            RequestedModel = "minimax-m2.7",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Say exactly: xkiro-smoke-ok" }
            }
        };

        var outcome = await provider.ExecuteAsync(route, request, CancellationToken.None);

        Assert.True(outcome.IsSuccess,
            $"Live xKiro provider call failed: {outcome.FailureValue?.SanitizedUpstreamMessage}");
        Assert.NotNull(outcome.Response);
        Assert.NotNull(outcome.Response!.Content);
    }
}
