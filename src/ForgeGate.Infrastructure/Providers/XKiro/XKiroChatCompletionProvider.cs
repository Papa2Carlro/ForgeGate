using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;
using Microsoft.Extensions.Configuration;

namespace ForgeGate.Infrastructure.Providers.XKiro;

/// <summary>
/// xKiro provider adapter — OpenAI-compatible endpoint with xKiro-specific
/// endpoint/auth configuration. Provider quirks localized at adapter boundary.
/// </summary>
public sealed class XKiroChatCompletionProvider : IChatCompletionProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiKey;

    public XKiroChatCompletionProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _endpoint = configuration["XKiro:Endpoint"]
            ?? "https://api.xkiro.com/v1/chat/completions";
        _apiKey = configuration["XKiro:ApiKey"]?.Trim() ?? string.Empty;
    }

    public async Task<ProviderExecutionOutcome> ExecuteAsync(
        ModelRoute route,
        CanonicalChatRequest request,
        CancellationToken cancellationToken)
    {
        // Build xKiro request using same OpenAI-compatible DTO shape
        var xkiroRequest = new OpenAICompatible.OpenAIChatCompletionRequest
        {
            Model = route.ProviderNativeModelId,
            Messages = request.Messages.Select(m => new OpenAICompatible.OpenAIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };

        var jsonContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(xkiroRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // xKiro authentication: Bearer token from runtime config
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }

        try
        {
            var response = await _httpClient.PostAsync(_endpoint, jsonContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Normalize through existing OpenAI-compatible failure mapper
                // (xKiro uses same error shape per adapter strategy)
                OpenAICompatible.OpenAIError? error = null;
                try
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var errorResponse = System.Text.Json.JsonSerializer.Deserialize<
                        OpenAICompatible.OpenAIErrorResponse>(
                        errorContent,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    error = errorResponse?.Error;
                }
                catch { /* Ignore deserialization errors */ }

                var failure = OpenAICompatible.OpenAIProviderFailureMapper.Map(
                    response.StatusCode, error, response.Headers);
                return ProviderExecutionOutcome.Failure(failure);
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var xkiroResponse = System.Text.Json.JsonSerializer.Deserialize<
                OpenAICompatible.OpenAIChatCompletionResponse>(
                responseContent,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (xkiroResponse == null)
            {
                return ProviderExecutionOutcome.Failure(new ProviderFailure
                {
                    Category = ProviderFailureCategory.MalformedResponse,
                    Retryability = ProviderFailureRetryability.NotRetryable,
                    Scope = ProviderFailureScope.Provider,
                    SanitizedUpstreamMessage = "Failed to deserialize xKiro provider response"
                });
            }

            // Map through existing canonical response mapper
            var canonicalResponse = OpenAICompatible.OpenAIChatCompletionProvider
                .MapToCanonicalResponseForTest(route, xkiroResponse);
            return ProviderExecutionOutcome.Success(canonicalResponse);
        }
        catch (HttpRequestException)
        {
            return ProviderExecutionOutcome.Failure(new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
                Scope = ProviderFailureScope.Provider
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return ProviderExecutionOutcome.Failure(new ProviderFailure
            {
                Category = ProviderFailureCategory.MalformedResponse,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Provider
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }
}
