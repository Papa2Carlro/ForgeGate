using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Domain.Providers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace ForgeGate.IntegrationTests;

/// <summary>
/// HTTP smoke test that verifies the complete server-side request path:
/// Client → Controller → Orchestrator → Provider → Response
///
/// Uses WebApplicationFactory to start the actual ASP.NET application
/// and replaces the OpenAI provider with a test double.
/// </summary>
public class ServerSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServerSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostChatCompletions_ReturnsSuccessfulResponse()
    {
        // Arrange - create client with overridden DI to use fake providers
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the real OpenAI provider with a test double
                var providerDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IChatCompletionProvider));
                if (providerDescriptor != null)
                    services.Remove(providerDescriptor);

                // Also replace streaming provider
                var streamingDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IStreamingChatCompletionProvider));
                if (streamingDescriptor != null)
                    services.Remove(streamingDescriptor);

                // Register fake providers
                services.AddScoped<IChatCompletionProvider>(_ => new FakeChatCompletionProvider(
                    new CanonicalChatResponse { Content = "Test response" }));
                services.AddScoped<IStreamingChatCompletionProvider>(_ => new FakeStreamingChatCompletionProvider());

                // Override routing configuration to include test routes
                var currentConfig = services.FirstOrDefault(d => d.ServiceType == typeof(IConfigureOptions<RoutingConfiguration>));
                if (currentConfig != null)
                {
                    services.Remove(currentConfig);
                }
                
                services.AddOptions<RoutingConfiguration>()
                    .Configure<IConfiguration>((config, configuration) =>
                    {
                        config.Routes = new List<ConfiguredRoute>
                        {
                            new ConfiguredRoute
                            {
                                RequestedModelAlias = "gpt-4",
                                ModelRoute = ModelRoute.FromIds(
                                    ProviderId.From("openai"),
                                    LogicalModelId.From("gpt-4"),
                                    ModelRouteId.From("openai:gpt-4")),
                                Enabled = true
                            }
                        };
                    });
            });
        }).CreateClient();

        // Act - send HTTP request
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Hello"}]
            }
            """;

        var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Assert - capture response body for debugging even on failure
        var responseBody = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            // Log the error for debugging
            System.Console.WriteLine($"Request failed with status: {response.StatusCode}");
            System.Console.WriteLine($"Response body: {responseBody}");
        }
        
        response.EnsureSuccessStatusCode();
    }
}
