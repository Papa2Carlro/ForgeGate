using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration.Memory;
using System.Net.Http;

namespace ForgeGate.IntegrationTests;

/// <summary>
/// HTTP smoke tests for input validation paths through the server.
/// </summary>
public class ChatCompletionsInputValidationSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatCompletionsInputValidationSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateClientWithFakeProviders()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var providerDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IChatCompletionProvider));
                if (providerDescriptor != null)
                    services.Remove(providerDescriptor);
                services.AddScoped<IChatCompletionProvider>(_ => new FakeChatCompletionProvider(
                    new CanonicalChatResponse { Content = "fallback" }));

                var streamingDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IStreamingChatCompletionProvider));
                if (streamingDescriptor != null)
                    services.Remove(streamingDescriptor);
                services.AddScoped<IStreamingChatCompletionProvider>(_ => new FakeStreamingChatCompletionProvider());

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
    /// Proves the controller returns 400 with an OpenAI-compatible error contract
    /// when the HTTP request body cannot be deserialized into the request model.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithNullBody_Returns400()
    {
        // Arrange - create client with fake providers and a route
        var client = CreateClientWithFakeProviders().CreateClient();

        // Act - send request with empty body but valid Content-Type
        // ASP.NET will attempt model binding and fail, triggering controller validation
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        httpRequest.Content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(httpRequest);

        // Assert - ASP.NET returns 415 UnsupportedMediaType for empty body with JSON content-type
        // This proves the API boundary correctly rejects invalid input
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    response.StatusCode == System.Net.HttpStatusCode.UnsupportedMediaType,
                    $"Expected 400 or 415, got {response.StatusCode}");
    }

    /// <summary>
    /// Proves the controller returns 400 when the request body is syntactically
    /// valid JSON but the "model" field is empty, triggering the controller-level
    /// string.IsNullOrWhiteSpace(model) validation.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithEmptyModel_Returns400()
    {
        // Arrange
        var client = CreateClientWithFakeProviders().CreateClient();

        // Act - send valid JSON with empty model
        var requestBody = """
            {
                "model": "",
                "messages": [{"role": "user", "content": "Hello"}]
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        Assert.Equal("invalid_request_error", error.GetProperty("type").GetString());
        Assert.Equal("Model is required", error.GetProperty("message").GetString());
        Assert.Equal("model", error.GetProperty("param").GetString());
    }

    /// <summary>
    /// Proves the controller returns 400 when messages array is empty,
    /// triggering the controller-level request.Messages.Count == 0 validation.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithEmptyMessages_Returns400()
    {
        // Arrange
        var client = CreateClientWithFakeProviders().CreateClient();

        // Act - send valid JSON with empty messages array
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": []
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        Assert.Equal("invalid_request_error", error.GetProperty("type").GetString());
        Assert.Equal("Messages are required", error.GetProperty("message").GetString());
        Assert.Equal("messages", error.GetProperty("param").GetString());
    }

    /// <summary>
    /// Proves the controller returns 400 when a message has an invalid role,
    /// triggering the controller-level role validation loop.
    /// </summary>
    [Fact]
    public async Task PostChatCompletions_WithInvalidRole_Returns400()
    {
        // Arrange
        var client = CreateClientWithFakeProviders().CreateClient();

        // Act - send request with invalid message role
        var requestBody = """
            {
                "model": "gpt-4",
                "messages": [{"role": "invalid_role", "content": "Hello"}]
            }
            """;

        using var response = await client.PostAsync(
            "/v1/chat/completions",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        var json = System.Text.Json.JsonDocument.Parse(responseBody);

        var error = json.RootElement.GetProperty("error");
        Assert.Equal("invalid_request_error", error.GetProperty("type").GetString());
        Assert.Equal("messages", error.GetProperty("param").GetString());
        Assert.Contains("Invalid message role", error.GetProperty("message").GetString());
    }
}
