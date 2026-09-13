using ForgeGate.Application.Chat;
using ForgeGate.Domain.Providers;
using Microsoft.Extensions.Configuration;

namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// Concrete provider adapter for OpenAI-compatible chat completions.
/// Handles HTTP communication and maps between OpenAI DTOs and canonical models.
/// </summary>
public sealed class OpenAIChatCompletionProvider : IChatCompletionProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    public OpenAIChatCompletionProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        // In a real implementation, this would come from configuration/secrets
        // For this slice, we'll use a default that can be overridden
        _endpoint = configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
    }

    /// <summary>
    /// Executes a chat completion request against the OpenAI-compatible provider.
    /// </summary>
    /// <param name="route">Explicit model route to execute against.</param>
    /// <param name="request">The canonical chat request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Application-owned normalized outcome.</returns>
    public async Task<ProviderExecutionOutcome> ExecuteAsync(
        ModelRoute route,
        CanonicalChatRequest request,
        CancellationToken cancellationToken)
    {
        // Map canonical request to provider-specific DTO
        var openaiRequest = MapToOpenAIRequest(route, request);

        // Serialize and send HTTP request
        var jsonContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(openaiRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // Add authorization header if API key is configured
        var apiKey = GetApiKeyFromConfiguration();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        // Execute HTTP request
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.PostAsync(_endpoint, jsonContent, cancellationToken);

            // Handle HTTP errors - normalize to application outcome
            if (!response.IsSuccessStatusCode)
            {
                OpenAIError? error = null;
                try
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var errorResponse = System.Text.Json.JsonSerializer.Deserialize<OpenAIErrorResponse>(
                        errorContent,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    error = errorResponse?.Error;
                }
                catch { /* Ignore deserialization errors */ }

                var failure = OpenAIProviderFailureMapper.Map(response.StatusCode, error, response.Headers);
                return ProviderExecutionOutcome.Failure(failure);
            }

            // Read and deserialize provider response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var openaiResponse = System.Text.Json.JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(
                responseContent,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (openaiResponse == null)
            {
                var failure = new ProviderFailure
            {
                Category = ProviderFailureCategory.MalformedResponse,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Provider,
                SanitizedUpstreamMessage = "Failed to deserialize provider response"
            };
            return ProviderExecutionOutcome.Failure(failure);
            }

            // Map provider response to canonical response
            var canonicalResponse = MapToCanonicalResponse(route, openaiResponse);
            return ProviderExecutionOutcome.Success(canonicalResponse);
        }
        catch (HttpRequestException)
        {
            var failure = new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
                Scope = ProviderFailureScope.Provider
            };
            return ProviderExecutionOutcome.Failure(failure);
        }
        catch (System.Text.Json.JsonException)
        {
            var failure = new ProviderFailure
            {
                Category = ProviderFailureCategory.MalformedResponse,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Provider
            };
            return ProviderExecutionOutcome.Failure(failure);
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation to preserve semantics
            throw;
        }
    }

    private static OpenAIChatCompletionRequest MapToOpenAIRequest(ModelRoute route, CanonicalChatRequest request)
    {
        return new OpenAIChatCompletionRequest
        {
            Model = route.ProviderNativeModelId,
            Messages = request.Messages.Select(m => new OpenAIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };
    }

    private static CanonicalChatResponse MapToCanonicalResponse(ModelRoute route, OpenAIChatCompletionResponse response)
    {
        // Extract the first choice's message content
        var content = response.Choices.FirstOrDefault()?.Message.Content
                     ?? string.Empty;

        return new CanonicalChatResponse
        {
            Content = content
        };
    }

    private string GetApiKeyFromConfiguration()
    {
        // In a real implementation, this would come from secure configuration/secrets
        // For this slice, we'll attempt to read from configuration but allow empty for keyless providers
        return ""; // Placeholder - would normally come from IConfiguration
    }
}