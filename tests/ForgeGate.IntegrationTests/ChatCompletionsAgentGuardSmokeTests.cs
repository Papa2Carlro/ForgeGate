using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using System.Net.Http;

namespace ForgeGate.IntegrationTests;

/// <summary>
/// HTTP smoke tests for Agent Guard integration through the server.
/// </summary>
public class ChatCompletionsAgentGuardSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatCompletionsAgentGuardSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateClientWithDenyAgentGuard()
    {
        return _factory.WithWebHostBuilder(builder =>
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
                    new CanonicalChatResponse
                    {
                        Content = "",
                        ToolCalls = new[] { new ToolCallInvocation { Id = "1", Name = "delete_file", Arguments = "/workspace/important.txt" } }
                    }));
                services.AddScoped<IStreamingChatCompletionProvider>(_ => new FakeStreamingChatCompletionProvider());

                // Replace AgentGuard with one that always denies
                services.AddSingleton<IAgentGuard>(_ => new DenyingAgentGuard());

                // Override routing configuration to include test routes
                var currentConfig = services.FirstOrDefault(d => d.ServiceType == typeof(IConfigureOptions<RoutingConfiguration>));
                if (currentConfig != null)
                    services.Remove(currentConfig);

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
        });
    }

    private WebApplicationFactory<Program> CreateClientWithApprovingAgentGuard()
    {
        return _factory.WithWebHostBuilder(builder =>
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
                    new CanonicalChatResponse
                    {
                        Content = "",
                        ToolCalls = new[] { new ToolCallInvocation { Id = "1", Name = "delete_file", Arguments = "/workspace/important.txt" } }
                    }));
                services.AddScoped<IStreamingChatCompletionProvider>(_ => new FakeStreamingChatCompletionProvider());

                // Replace AgentGuard with one that requires human approval
                services.AddSingleton<IAgentGuard>(_ => new ApprovingAgentGuard());

                // Override routing configuration to include test routes
                var currentConfig = services.FirstOrDefault(d => d.ServiceType == typeof(IConfigureOptions<RoutingConfiguration>));
                if (currentConfig != null)
                    services.Remove(currentConfig);

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
        });
    }

    /// <summary>
    /// Proves that when Agent Guard denies a tool call, the HTTP response reflects
    /// the policy denial with correct status code and error contract.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithAgentGuardDeny_Returns403()
    {
        // Arrange - create client with fake providers and a deny-gatekeeping agent guard
        var client = CreateClientWithDenyAgentGuard().CreateClient();

        // Act - send HTTP request with tool call that will be denied
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Delete this file"}]
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Assert - verify the denial contract
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        // Verify error contract
        var error = json.RootElement.GetProperty("error");
        Assert.Equal("agent_guard_denied", error.GetProperty("type").GetString());
        Assert.Equal("agent_guard_denied", error.GetProperty("code").GetString());
        Assert.Contains("denied by policy", error.GetProperty("message").GetString());
    }

    /// <summary>
    /// Proves that when Agent Guard requires human approval for a tool call,
    /// the HTTP response reflects the approval requirement with correct status code.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithAgentGuardRequiresApproval_Returns409()
    {
        // Arrange - create client with fake providers and an approval-requiring agent guard
        var client = CreateClientWithApprovingAgentGuard().CreateClient();

        // Act - send HTTP request with tool call that requires approval
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Delete this file"}]
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Assert - verify the approval requirement contract
        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        // Verify error contract
        var error = json.RootElement.GetProperty("error");
        Assert.Equal("agent_guard_requires_human_approval", error.GetProperty("type").GetString());
        Assert.Equal("agent_guard_requires_human_approval", error.GetProperty("code").GetString());
        Assert.Contains("requires human approval", error.GetProperty("message").GetString());
    }
}
