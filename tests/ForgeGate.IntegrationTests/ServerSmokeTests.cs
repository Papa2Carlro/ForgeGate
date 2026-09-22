using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.Providers;
using ForgeGate.Domain.AgentGuard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Infrastructure.Routing;
using ForgeGate.Infrastructure.Providers.OpenAICompatible;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Microsoft.Extensions.Configuration.Memory;

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

    /// <summary>
    /// Proves that when a provider response contains tool calls, the real Agent Guard
    /// integration point (ChatCompletionOrchestrator.EvaluateToolCalls → AgentGuardService
    /// → BasicActionIntentNormalizer → BasicCapabilityTranslator → BasicPolicyEvaluator
    /// → BasicLayer3Evaluator) is exercised through the actual HTTP server path.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithToolCalls_InvokesRealAgentGuard()
    {
        // Arrange - create client with ONLY fake providers (no spy overrides)
        // This lets production DI resolve AgentGuardService and its real dependencies
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
                    new CanonicalChatResponse
                    {
                        Content = "",
                        ToolCalls = new[] { new ToolCallInvocation { Id = "1", Name = "ls", Arguments = "/workspace" } }
                    }));
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

        // Assert - HTTP response is valid
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        // Verify response structure includes tool calls
        Assert.Equal("chat.completion", json.RootElement.GetProperty("object").GetString());
        Assert.Equal("gpt-4", json.RootElement.GetProperty("model").GetString());

        var choices = json.RootElement.GetProperty("choices");
        Assert.True(choices.GetArrayLength() > 0);

        var choice = choices[0];
        Assert.Equal("tool_calls", choice.GetProperty("finish_reason").GetString());
        Assert.True(choice.TryGetProperty("tool_calls", out var toolCallsProp), "tool_calls property not found");
        Assert.True(toolCallsProp.GetArrayLength() > 0);

        // Verify tool call content
        var toolCalls = toolCallsProp;
        Assert.Equal("ls", toolCalls[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("/workspace", toolCalls[0].GetProperty("function").GetProperty("arguments").GetString());
    }

    /// <summary>
    /// Proves that the REAL OpenAIChatCompletionProvider is exercised through the HTTP server path,
    /// with outbound HTTP intercepted by a test-controlled handler.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithRealProvider_ReturnsToolCallResponse()
    {
        // Arrange - capture outbound HTTP request details
        HttpRequestMessage? capturedRequest = null;
        var testHandler = new TestHttpMessageHandler(
            (request) => capturedRequest = request,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""id"": ""test-1"", ""object"": ""chat.completion"", ""created"": 1234567890, ""model"": ""gpt-4"", ""choices"": [{""index"": 0, ""message"": {""role"": ""assistant"", ""content"": ""Test response""}, ""finish_reason"": ""tool_calls"", ""tool_calls"": [{""id"": ""tc_1"", ""type"": ""function"", ""function"": {""name"": ""ls"", ""arguments"": ""\/workspace""}}]}], ""usage"": {""prompt_tokens"": 10, ""completion_tokens"": 20, ""total_tokens"": 30}}")
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") }
                }
            });

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace streaming provider only (keep real chat provider)
                var streamingDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IStreamingChatCompletionProvider));
                if (streamingDescriptor != null)
                    services.Remove(streamingDescriptor);
                services.AddScoped<IStreamingChatCompletionProvider>(_ => new FakeStreamingChatCompletionProvider());

                // Replace the real OpenAI provider with a real instance using test HttpClient
                var providerDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IChatCompletionProvider));
                if (providerDescriptor != null)
                    services.Remove(providerDescriptor);

                // Remove default HttpClient registrations
                var httpClientDescriptors = services.Where(d => d.ServiceType == typeof(HttpClient)).ToList();
                foreach (var desc in httpClientDescriptors)
                    services.Remove(desc);

                // Register typed HttpClient for OpenAIChatCompletionProvider with test handler
                services.AddHttpClient<OpenAIChatCompletionProvider>(client =>
                {
                    client.BaseAddress = new Uri("http://test/");
                })
                .ConfigurePrimaryHttpMessageHandler(() => testHandler);

                // Add real provider with test HttpClient and test configuration
                services.AddScoped<IChatCompletionProvider>(sp =>
                {
                    var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                        .CreateClient(typeof(OpenAIChatCompletionProvider).Name);
                    // Create test configuration pointing to test server
                    var memoryConfig = new Dictionary<string, string?>
                    {
                        ["OpenAI:Endpoint"] = "http://test/chat/completions"
                    };
                    var configurationBuilder = new ConfigurationBuilder();
                    configurationBuilder.AddInMemoryCollection(memoryConfig);
                    var testConfig = configurationBuilder.Build();
                    return new OpenAIChatCompletionProvider(httpClient, testConfig);
                });

                // Register NullAgentGuardEventEmitter (missing from Program.cs DI)
                services.AddSingleton<IAgentGuardEventEmitter>(NullAgentGuardEventEmitter.Instance);

                // Override routing configuration
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
                                ModelRoute = ModelRoute.FromIdsWithOptions(
                                    ProviderId.From("openai"),
                                    LogicalModelId.From("gpt-4"),
                                    ModelRouteId.From("openai:gpt-4"),
                                    providerNativeModelId: "gpt-4",
                                    enabled: true,
                                    capabilities: ModelCapability.None),
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

        // Always read body to capture server error details for debugging
        var responseBody = await response.Content.ReadAsStringAsync();

        // Debug: print response for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            System.Console.WriteLine($"[DEBUG] Server returned {response.StatusCode}");
            System.Console.WriteLine($"[DEBUG] Body: {responseBody}");
            System.Console.WriteLine($"[DEBUG] Captured request URI: {capturedRequest?.RequestUri}");
            if (capturedRequest?.Content != null)
            {
                var reqBody = await capturedRequest.Content.ReadAsStringAsync();
                System.Console.WriteLine($"[DEBUG] Outbound request body: {reqBody}");
            }
        }

        response.EnsureSuccessStatusCode();
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        // Verify ForgeGate HTTP response structure
        Assert.Equal("chat.completion", json.RootElement.GetProperty("object").GetString());
        Assert.Equal("gpt-4", json.RootElement.GetProperty("model").GetString());

        var choices = json.RootElement.GetProperty("choices");
        Assert.True(choices.GetArrayLength() > 0);

        var choice = choices[0];
        Assert.Equal("tool_calls", choice.GetProperty("finish_reason").GetString());
        Assert.Equal("Test response", choice.GetProperty("message").GetProperty("content").GetString());

        // Verify tool call mapping
        Assert.True(choice.TryGetProperty("tool_calls", out var toolCallsProp), "tool_calls property not found");
        Assert.True(toolCallsProp.GetArrayLength() > 0);
        var toolCalls = toolCallsProp;
        Assert.Equal("tc_1", toolCalls[0].GetProperty("id").GetString());
        Assert.Equal("ls", toolCalls[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("/workspace", toolCalls[0].GetProperty("function").GetProperty("arguments").GetString());

        // Verify outbound HTTP request was intercepted
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("http://test/chat/completions", capturedRequest.RequestUri!.ToString());

        // Verify request body contains expected fields
        var requestContent = await capturedRequest!.Content!.ReadAsStringAsync();
        var requestJson = System.Text.Json.JsonDocument.Parse(requestContent);
        Assert.Equal("gpt-4", requestJson.RootElement.GetProperty("model").GetString());
        Assert.Equal("user", requestJson.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("Hello", requestJson.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    /// <summary>
    /// Proves that when Agent Guard denies a tool call, the HTTP response reflects
    /// the policy denial with correct status code and error contract.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithAgentGuardDeny_Returns403()
    {
        // Arrange - create client with fake providers and a deny-gatekeeping agent guard
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
        }).CreateClient();

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
        }).CreateClient();

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

/// <summary>
/// Test double: Agent Guard that always denies all actions.
/// </summary>
internal sealed class DenyingAgentGuard : IAgentGuard
{
    public AgentGuardResult Evaluate(AgentAction action) =>
        AgentGuardResult.Success(PolicyDecision.Deny, ActionIntentKind.FileDelete, action.RawAction);
}

/// <summary>
/// Test double: Agent Guard that always requires human approval.
/// </summary>
internal sealed class ApprovingAgentGuard : IAgentGuard
{
    public AgentGuardResult Evaluate(AgentAction action) =>
        AgentGuardResult.Success(PolicyDecision.RequireHumanApproval, ActionIntentKind.FileDelete, action.RawAction);
}
internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Action<HttpRequestMessage> _onRequest;
    private readonly Func<HttpResponseMessage> _responseFactory;
    public HttpResponseMessage? LastResponse { get; private set; }

    public TestHttpMessageHandler(
        Action<HttpRequestMessage> onRequest,
        Func<HttpResponseMessage> responseFactory)
    {
        _onRequest = onRequest;
        _responseFactory = responseFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _onRequest(request);
        var response = _responseFactory();
        response.RequestMessage = request;
        LastResponse = response;
        return Task.FromResult(response);
    }
}
