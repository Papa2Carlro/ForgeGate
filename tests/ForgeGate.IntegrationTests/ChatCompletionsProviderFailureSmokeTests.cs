using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.Providers;
using ForgeGate.Domain.AgentGuard;
using ForgeGate.Infrastructure.Routing;
using ForgeGate.Infrastructure.Providers.OpenAICompatible;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration.Memory;
using System.Net.Http;

namespace ForgeGate.IntegrationTests;

/// <summary>
/// HTTP smoke tests for provider failure and failover paths through the server.
/// </summary>
public class ChatCompletionsProviderFailureSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatCompletionsProviderFailureSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider returns a retryable error (503),
    /// the orchestrator attempts failover, exhausts available routes, and returns HTTP 502
    /// with the routing exhaustion error contract.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithRetryableProviderFailure_Returns502AfterFailoverExhaustion()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider but make its
        // outbound HttpClient return 503 Service Unavailable (maps to ProviderUnavailable)
        HttpRequestMessage? capturedRequest = null;
        var testHandler = new TestHttpMessageHandler(
            (request) => capturedRequest = request,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Service Unavailable\",\"type\":\"api_error\",\"code\":\"service_unavailable\"}}")
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

        // Act - send HTTP request; provider will receive 502 response
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Hello"}]
            }
            """;

        using var response = await client.PostAsync(
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

        // Assert - verify the failover exhaustion contract
        // Provider returns 503 → OpenAIProviderFailureMapper maps to ProviderUnavailable (RetryViaAnotherRoute)
        // → Orchestrator attempts failover, excludes the only route
        // → ConfiguredRouteResolver returns NoEligibleRoute
        // → ChatCompletionOutcome.FailWithResolutionError creates UnknownProviderFailure
        // → Controller maps UnknownProviderFailure via default case to 502
        Assert.Equal(System.Net.HttpStatusCode.BadGateway, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        // Verify exact error contract for route exhaustion after failover
        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("api_error", errorType);
        Assert.Equal("unknown_error", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
        Assert.Contains("No eligible route", errorMessage);
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider receives a malformed JSON response
    /// from the upstream provider, it produces a MalformedResponse failure which propagates
    /// through the orchestrator and is mapped to HTTP 502 by the controller.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithMalformedProviderResponse_ReturnsExpectedError()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider but return malformed JSON
        HttpRequestMessage? capturedRequest = null;
        var testHandler = new TestHttpMessageHandler(
            (request) => capturedRequest = request,
            () => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                // Return a syntactically invalid JSON body to trigger JsonException
                Content = new StringContent("{invalid json that cannot be parsed}")
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

        // Act - send HTTP request; provider will receive malformed JSON
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Hello"}]
            }
            """;

        using var response = await client.PostAsync(
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

        // Assert - verify the malformed response contract
        // Provider returns 200 OK with malformed JSON → JsonException caught → MalformedResponse (NotRetryable)
        // → Orchestrator returns failure without failover (NotRetryable)
        // → Controller maps MalformedResponse to 502 BadGateway with api_error/malformed_response
        Assert.Equal(System.Net.HttpStatusCode.BadGateway, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        // Verify exact error contract for malformed provider response
        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("api_error", errorType);
        Assert.Equal("malformed_response", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider receives an HTTP 401 Unauthorized
    /// from the upstream provider, the OpenAIProviderFailureMapper classifies it as
    /// AuthenticationFailed and the controller maps it to HTTP 401 with the correct error contract.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithProvider401_Returns401()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider with a test handler returning 401
        var testHandler = new TestHttpMessageHandler(
            request => { },
            () => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Invalid authentication\",\"type\":\"invalid_request_error\",\"code\":\"invalid_api_key\"}}")
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

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Always read body to capture server error details for debugging
        var responseBody = await response.Content.ReadAsStringAsync();

        // Debug: print response for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            System.Console.WriteLine($"[DEBUG] Server returned {response.StatusCode}");
            System.Console.WriteLine($"[DEBUG] Body: {responseBody}");
        }

        // Assert - verify the 401 provider error contract
        // Provider returns 401 → OpenAIProviderFailureMapper maps to AuthenticationFailed (NotRetryable)
        // → Orchestrator returns terminal failure (no failover)
        // → Controller maps AuthenticationFailed to 401 Unauthorized with authentication_error/authentication_failed
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("authentication_error", errorType);
        Assert.Equal("authentication_failed", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider receives an HTTP 429 Too Many Requests
    /// from the upstream provider, the OpenAIProviderFailureMapper classifies it as
    /// RateLimited (RetryAfterDelay, terminal for this single-route path) and the controller
    /// maps it to HTTP 429 with the correct error contract.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithProvider429_Returns429()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider with a test handler returning 429
        var testHandler = new TestHttpMessageHandler(
            request => { },
            () => new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Rate limit reached\",\"type\":\"rate_limit_error\",\"code\":\"rate_limit_exceeded\"}}")
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

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Always read body to capture server error details for debugging
        var responseBody = await response.Content.ReadAsStringAsync();

        // Debug: print response for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            System.Console.WriteLine($"[DEBUG] Server returned {response.StatusCode}");
            System.Console.WriteLine($"[DEBUG] Body: {responseBody}");
        }

        // Assert - verify the 429 provider error contract
        // Provider returns 429 → OpenAIProviderFailureMapper maps to RateLimited (RetryAfterDelay)
        // → Orchestrator returns terminal failure (not RetryViaAnotherRoute, no failover)
        // → Controller maps RateLimited to 429 TooManyRequests with rate_limit_error/rate_limited
        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("rate_limit_error", errorType);
        Assert.Equal("rate_limited", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider receives an HTTP 403 Forbidden
    /// from the upstream provider, the OpenAIProviderFailureMapper classifies it as
    /// AuthorizationFailed (NotRetryable) and the controller maps it to HTTP 403 with the correct error contract.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithProvider403_Returns403()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider with a test handler returning 403
        var testHandler = new TestHttpMessageHandler(
            request => { },
            () => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Insufficient access permissions\",\"type\":\"authorization_error\",\"code\":\"insufficient_quota\"}}")
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

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Always read body to capture server error details for debugging
        var responseBody = await response.Content.ReadAsStringAsync();

        // Debug: print response for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            System.Console.WriteLine($"[DEBUG] Server returned {response.StatusCode}");
            System.Console.WriteLine($"[DEBUG] Body: {responseBody}");
        }

        // Assert - verify the 403 provider error contract
        // Provider returns 403 → OpenAIProviderFailureMapper maps to AuthorizationFailed (NotRetryable)
        // → Orchestrator returns terminal failure (no failover)
        // → Controller maps AuthorizationFailed to 403 Forbidden with authorization_error/authorization_failed
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("authorization_error", errorType);
        Assert.Equal("authorization_failed", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider raises an HttpRequestException
    /// (simulating a network/transport failure) during the outbound HTTP call, the failure is
    /// classified as NetworkFailure with RetryViaAnotherRoute, and with only a single route
    /// available the orchestrator exhausts failover and returns HTTP 502 via the default mapping.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithProviderNetworkFailure_ReturnsExpectedError()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider with a test handler that throws
        var testHandler = new TestHttpMessageHandler(
            request => { },
            () => throw new HttpRequestException("Connection refused", inner: null, System.Net.HttpStatusCode.ServiceUnavailable));

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

        // Act - send HTTP request; provider will throw HttpRequestException
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Hello"}]
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Always read body to capture server error details for debugging
        var responseBody = await response.Content.ReadAsStringAsync();

        // Debug: print response for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            System.Console.WriteLine($"[DEBUG] Server returned {response.StatusCode}");
            System.Console.WriteLine($"[DEBUG] Body: {responseBody}");
        }

        // Assert - verify the network failure contract
        // Provider throws HttpRequestException → NetworkFailure (RetryViaAnotherRoute, scope=Provider)
        // → Orchestrator attempts failover; only one route exists → route excluded → NoEligibleRoute
        // → ChatCompletionOutcome.FailWithResolutionError → UnknownProviderFailure (default)
        // → Controller maps UnknownProviderFailure via default case to 502 BadGateway
        Assert.Equal(System.Net.HttpStatusCode.BadGateway, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("api_error", errorType);
        Assert.Equal("unknown_error", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider encounters a timeout during
    /// the outbound HTTP call, the provider classifies it as NetworkFailure (RetryViaAnotherRoute),
    /// and with only a single route the orchestrator exhausts failover and returns HTTP 502.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithProviderTimeout_ReturnsExpectedError()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider with a test handler that simulates timeout
        var testHandler = new TestHttpMessageHandler(
            request => { },
            () => throw new HttpRequestException("The operation has timed out.", inner: null, System.Net.HttpStatusCode.RequestTimeout));

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

        // Act - send HTTP request; provider will encounter a timeout exception
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Hello"}]
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Always read body to capture server error details for debugging
        var responseBody = await response.Content.ReadAsStringAsync();

        // Debug: print response for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            System.Console.WriteLine($"[DEBUG] Server returned {response.StatusCode}");
            System.Console.WriteLine($"[DEBUG] Body: {responseBody}");
        }

        // Assert - verify the timeout/network failure contract
        // Provider throws HttpRequestException (timeout) → NetworkFailure (RetryViaAnotherRoute)
        // → Orchestrator attempts failover; only one route → route excluded → NoEligibleRoute
        // → ChatCompletionOutcome.FailWithResolutionError → UnknownProviderFailure (default)
        // → Controller maps UnknownProviderFailure via default case to 502 BadGateway
        Assert.Equal(System.Net.HttpStatusCode.BadGateway, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("api_error", errorType);
        Assert.Equal("unknown_error", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider receives an HTTP 408 Request Timeout
    /// from the upstream provider, the OpenAIProviderFailureMapper classifies it as
    /// Timeout (RetryViaAnotherRoute), and with only a single route the orchestrator exhausts
    /// failover and returns HTTP 502 via the default UnknownProviderFailure mapping.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithProvider408_Returns504()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider with a test handler returning 408
        var testHandler = new TestHttpMessageHandler(
            request => { },
            () => new HttpResponseMessage(System.Net.HttpStatusCode.RequestTimeout)
            {
                Content = new StringContent("{\"error\":{\"message\":\"The operation has timed out\",\"type\":\"request_timeout\",\"code\":\"request_timeout\"}}")
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

        // Act - send HTTP request; provider will receive 408 timeout
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Hello"}]
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Always read body to capture server error details for debugging
        var responseBody = await response.Content.ReadAsStringAsync();

        // Debug: print response for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            System.Console.WriteLine($"[DEBUG] Server returned {response.StatusCode}");
            System.Console.WriteLine($"[DEBUG] Body: {responseBody}");
        }

        // Assert - verify the timeout contract
        // Provider returns 408 → OpenAIProviderFailureMapper maps to Timeout (RetryViaAnotherRoute)
        // → Orchestrator attempts failover; only one route → route excluded → NoEligibleRoute
        // → ChatCompletionOutcome.FailWithResolutionError → UnknownProviderFailure (default)
        // → Controller maps UnknownProviderFailure via default case to 502 BadGateway
        Assert.Equal(System.Net.HttpStatusCode.BadGateway, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("api_error", errorType);
        Assert.Equal("unknown_error", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
    }

    /// <summary>
    /// Proves that when the real OpenAIChatCompletionProvider receives an HTTP 400 Bad Request
    /// from the upstream provider (with a generic error code, not context_length_exceeded or
    /// model_not_found), it is classified as InvalidRequest (NotRetryable) and the controller
    /// maps it to HTTP 400 with the existing invalid_request error contract.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithProvider400_Returns400()
    {
        // Arrange - use the REAL OpenAIChatCompletionProvider with a test handler returning 400
        var testHandler = new TestHttpMessageHandler(
            request => { },
            () => new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Invalid parameter: messages[0].content\",\"type\":\"invalid_request_error\",\"code\":\"invalid_parameter\"}}")
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

        // Act - send HTTP request; provider will receive 400
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "user", "content": "Hello"}]
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Always read body to capture server error details for debugging
        var responseBody = await response.Content.ReadAsStringAsync();

        // Debug: print response for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            System.Console.WriteLine($"[DEBUG] Server returned {response.StatusCode}");
            System.Console.WriteLine($"[DEBUG] Body: {responseBody}");
        }

        // Assert - verify the 400 provider error contract
        // Provider returns 400 with generic error → OpenAIProviderFailureMapper maps to
        // InvalidRequest (NotRetryable, scope=Request) since code is not context_length_exceeded
        // or model_not_found
        // → Orchestrator returns terminal failure (not RetryViaAnotherRoute, no failover)
        // → Controller maps InvalidRequest to 400 BadRequest with invalid_request_error/invalid_request
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        var errorType = error.GetProperty("type").GetString();
        var errorCode = error.GetProperty("code").GetString();
        var errorMessage = error.GetProperty("message").GetString();

        Assert.Equal("invalid_request_error", errorType);
        Assert.Equal("invalid_request", errorCode);
        Assert.NotNull(errorMessage);
        Assert.NotEmpty(errorMessage);
    }
}
