using ForgeGate.Application.Chat;
using ForgeGate.Domain.Chat;
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
    /// <param name="request">The canonical chat request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The canonical chat response from the provider.</returns>
    public async Task<CanonicalChatResponse> ExecuteAsync(
        CanonicalChatRequest request,
        CancellationToken cancellationToken)
    {
        // Map canonical request to provider-specific DTO
        var openaiRequest = MapToOpenAIRequest(request);
        
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
        var response = await _httpClient.PostAsync(_endpoint, jsonContent, cancellationToken);
        
        // Handle HTTP errors
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ProviderRequestFailedException(
                $"Provider request failed with status {(int)response.StatusCode}: {response.ReasonPhrase}",
                errorContent);
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
            throw new ProviderResponseUnusableException("Failed to deserialize provider response");
        }

        // Map provider response to canonical response
        return MapToCanonicalResponse(openaiResponse);
    }

    private static OpenAIChatCompletionRequest MapToOpenAIRequest(CanonicalChatRequest request)
    {
        return new OpenAIChatCompletionRequest
        {
            Model = request.Model,
            Messages = request.Messages.Select(m => new OpenAIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };
    }

    private static CanonicalChatResponse MapToCanonicalResponse(OpenAIChatCompletionResponse response)
    {
        // Extract the first choice's message content
        var content = response.Choices.FirstOrDefault()?.Message.Content 
                     ?? string.Empty;

        return new CanonicalChatResponse
        {
            Model = response.Model,
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

/// <summary>
/// Exception thrown when provider request fails at the HTTP level.
/// </summary>
public sealed class ProviderRequestFailedException : Exception
{
    public ProviderRequestFailedException(string message, string? providerResponse = null)
        : base(message)
    {
        ProviderResponse = providerResponse;
    }

    public string? ProviderResponse { get; }
}

/// <summary>
/// Exception thrown when provider response is malformed or unusable.
/// </summary>
public sealed class ProviderResponseUnusableException : Exception
{
    public ProviderResponseUnusableException(string message)
        : base(message)
    {
    }
}