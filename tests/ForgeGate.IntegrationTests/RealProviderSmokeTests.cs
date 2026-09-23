using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Domain.Providers;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ForgeGate.IntegrationTests;

/// <summary>
/// Explicit opt-in live smoke harness for real external provider execution.
/// Only runs when FORGEGATE_RUN_REAL_PROVIDER_SMOKE=true is set in the environment.
/// Uses the real OpenAIChatCompletionProvider with runtime-configured endpoint and API key.
/// </summary>
public class RealProviderSmokeTests
{
    private static bool IsLiveSmokeEnabled()
    {
        var value = Environment.GetEnvironmentVariable("FORGEGATE_RUN_REAL_PROVIDER_SMOKE");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task RealProviderSmoke_ExplicitOptIn_ExecutesFullPipeline()
    {
        Skip.IfNot(IsLiveSmokeEnabled(),
            "Live provider smoke skipped: set FORGEGATE_RUN_REAL_PROVIDER_SMOKE=true to run.");

        // Read runtime configuration only from environment (no committed secrets)
        var endpoint = Environment.GetEnvironmentVariable("OpenAI__Endpoint")
            ?? throw new InvalidOperationException(
                "OpenAI__Endpoint environment variable is required for live smoke.");
        var apiKey = Environment.GetEnvironmentVariable("OpenAI__ApiKey")
            ?? throw new InvalidOperationException(
                "OpenAI__ApiKey environment variable is required for live smoke.");

        // Build real provider with real HTTP client (no fake handler)
        var config = new MemoryConfigurationSource();
        var memoryConfig = new MemoryConfigurationProvider(config);
        memoryConfig.Add("OpenAI:Endpoint", endpoint);
        memoryConfig.Add("OpenAI:ApiKey", apiKey);
        var configuration = new ConfigurationRoot(new[] { memoryConfig });

        var httpClient = new HttpClient();
        var provider = new ForgeGate.Infrastructure.Providers.OpenAICompatible.OpenAIChatCompletionProvider(
            httpClient, configuration);

        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4"));

        var request = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Say exactly: smoke-ok" }
            }
        };

        // Execute through real provider
        var outcome = await provider.ExecuteAsync(route, request, CancellationToken.None);

        // Prove real external call happened: success with canonical response shape
        Assert.True(outcome.IsSuccess, $"Live provider call failed: {outcome.FailureValue?.SanitizedUpstreamMessage}");
        Assert.NotNull(outcome.Response);
        Assert.NotNull(outcome.Response!.Content);
    }
}
