using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Providers.XKiro;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Net.Http;

namespace ForgeGate.Application.Tests;

public class XKiroProviderTests
{
    private sealed class InMemoryConfig : IConfiguration
    {
        private readonly Dictionary<string, string> _values;
        public InMemoryConfig(Dictionary<string, string> values) => _values = values;
        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set => _values[key] = value!;
        }
        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
        public IConfigurationSection GetSection(string key) => new InMemorySection(_values, key);
        private sealed class InMemorySection : IConfigurationSection
        {
            private readonly Dictionary<string, string> _values; private readonly string _key;
            public InMemorySection(Dictionary<string, string> values, string key) { _values = values; _key = key; }
            public string Key => _key; public string Path => _key; public string? Value { get => _values.TryGetValue(_key, out var v) ? v : null; set => _values[_key] = value!; }
            public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
            public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
            public IConfigurationSection GetSection(string key) => new InMemorySection(_values, $"{_key}:{key}");
            public string? this[string key]
            {
                get => new InMemorySection(_values, $"{_key}:{key}").Value;
                set => new InMemorySection(_values, $"{_key}:{key}").Value = value;
            }
        }
    }

    private static IConfiguration CreateXKiroConfig(string endpoint, string apiKey)
    {
        return new InMemoryConfig(new Dictionary<string, string>
        {
            ["XKiro:Endpoint"] = endpoint,
            ["XKiro:ApiKey"] = apiKey
        });
    }

    [Fact]
    public async Task ExecuteAsync_SendsXKiroRequestWithCorrectEndpointAndAuth()
    {
        var route = ModelRoute.FromIds(
            ProviderId.From("xkiro"),
            LogicalModelId.From("minimax-m2.7"),
            ModelRouteId.From("xkiro:minimax-m2.7"));
        var request = new CanonicalChatRequest
        {
            RequestedModel = "minimax-m2.7",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        string? capturedEndpoint = null;
        string? capturedAuth = null;
        var handler = new CapturingHttpMessageHandler(r =>
        {
            capturedEndpoint = r.RequestUri?.ToString();
            capturedAuth = r.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":\"xkiro-test\",\"object\":\"chat.completion\",\"created\":1,\"model\":\"minimax-m2.7\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"XKiro response\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}")
            };
        });

        var httpClient = new HttpClient(handler);
        var config = CreateXKiroConfig("https://api.xkiro.com/v1/chat/completions", "xk-test-key-789");
        var provider = new XKiroChatCompletionProvider(httpClient, config);

        var outcome = await provider.ExecuteAsync(route, request, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Contains("xkiro.com", capturedEndpoint ?? "");
        Assert.Equal("Bearer xk-test-key-789", capturedAuth);
        Assert.Equal("XKiro response", outcome.Response?.Content);
    }

    [Fact]
    public async Task ExecuteAsync_NoAuthHeader_WhenApiKeyEmpty()
    {
        var route = ModelRoute.FromIds(
            ProviderId.From("xkiro"),
            LogicalModelId.From("minimax-m2.7"),
            ModelRouteId.From("xkiro:minimax-m2.7"));
        var request = new CanonicalChatRequest
        {
            RequestedModel = "minimax-m2.7",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        string? capturedAuth = null;
        var handler = new CapturingHttpMessageHandler(r =>
        {
            capturedAuth = r.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":\"xkiro-test\",\"object\":\"chat.completion\",\"created\":1,\"model\":\"minimax-m2.7\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"Hi\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1,\"total_tokens\":2}}")
            };
        });

        var httpClient = new HttpClient(handler);
        var config = CreateXKiroConfig("https://api.xkiro.com/v1/chat/completions", "");
        var provider = new XKiroChatCompletionProvider(httpClient, config);

        var outcome = await provider.ExecuteAsync(route, request, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Null(capturedAuth);
    }

    [Fact]
    public async Task ExecuteAsync_ApiKeyNotExposedInFailureMessage()
    {
        var route = ModelRoute.FromIds(
            ProviderId.From("xkiro"),
            LogicalModelId.From("minimax-m2.7"),
            ModelRouteId.From("xkiro:minimax-m2.7"));
        var request = new CanonicalChatRequest
        {
            RequestedModel = "minimax-m2.7",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        string? capturedAuth = null;
        var handler = new CapturingHttpMessageHandler(r =>
        {
            capturedAuth = r.Headers.Authorization?.ToString();
            throw new HttpRequestException("Connection refused");
        });

        var httpClient = new HttpClient(handler);
        var config = CreateXKiroConfig("https://api.xkiro.com/v1/chat/completions", "xk-secret-key-999");
        var provider = new XKiroChatCompletionProvider(httpClient, config);

        var outcome = await provider.ExecuteAsync(route, request, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("Bearer xk-secret-key-999", capturedAuth);
        Assert.DoesNotContain("xk-secret-key-999", outcome.FailureValue?.SanitizedUpstreamMessage ?? string.Empty);
    }

    private class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
