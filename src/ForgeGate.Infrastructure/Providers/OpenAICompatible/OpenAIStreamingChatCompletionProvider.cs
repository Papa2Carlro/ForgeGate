using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Domain.Providers;
using Microsoft.Extensions.Configuration;

namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// Production streaming provider adapter for OpenAI-compatible chat completions.
/// Handles streaming HTTP requests and normalizes SSE chunks into application-level chunks.
/// </summary>
public sealed class OpenAIStreamingChatCompletionProvider : IStreamingChatCompletionProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly StreamingToolCallAccumulator _toolCallAccumulator = new();

    public OpenAIStreamingChatCompletionProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _endpoint = configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
    }

    public async Task<StreamingExecutionOutcome> ExecuteStreamingAsync(
        ModelRoute route,
        StreamingChatRequest request,
        StreamingBuffer buffer,
        CancellationToken cancellationToken)
    {
        if (route == null)
            throw new ArgumentNullException(nameof(route));
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        var openaiRequest = MapToOpenAIRequest(route, request);
        var jsonContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(openaiRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        SetAuthHeader();

        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.PostAsync(_endpoint, jsonContent, cancellationToken);

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
                return StreamingExecutionOutcome.Failure(failure);
            }

            // Read the streaming response as SSE text
            var streamContent = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(streamContent);

            string? line;
            var chunks = new List<string>();
            string? currentEvent = null;
            string? currentData = null;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(line))
                {
                    // Empty line signals end of SSE event
                    if (currentEvent == "message" || currentEvent == null)
                    {
                        if (!string.IsNullOrEmpty(currentData))
                        {
                            TryParseAndBufferChunk(currentData, buffer, chunks, _toolCallAccumulator);
                        }
                    }
                    currentEvent = null;
                    currentData = null;
                    continue;
                }

                if (line.StartsWith("event:"))
                {
                    currentEvent = line.Substring(6).Trim();
                }
                else if (line.StartsWith("data:"))
                {
                    currentData = line.Substring(5).Trim();
                }
            }

            if (chunks.Count > 0)
            {
                buffer.Commit();
            }

            // Extract accumulated tool calls
            var toolCalls = _toolCallAccumulator.GetAccumulatedCalls();

            return StreamingExecutionOutcome.Success(
                chunks.AsReadOnly(), 
                buffer.IsCommitted, 
                route,
                toolCalls.Count > 0 ? toolCalls : null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            var failure = new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
                Scope = ProviderFailureScope.Provider,
                SanitizedUpstreamMessage = "Network failure during streaming"
            };
            return StreamingExecutionOutcome.Failure(failure);
        }
        catch (System.Text.Json.JsonException)
        {
            var failure = new ProviderFailure
            {
                Category = ProviderFailureCategory.MalformedResponse,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Provider,
                SanitizedUpstreamMessage = "Malformed streaming response"
            };
            return StreamingExecutionOutcome.Failure(failure);
        }
    }

    private void SetAuthHeader()
    {
        var apiKey = GetApiKeyFromConfiguration();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    private static void TryParseAndBufferChunk(
        string data, 
        StreamingBuffer buffer, 
        List<string> chunks, 
        StreamingToolCallAccumulator accumulator)
    {
        try
        {
            var chunk = System.Text.Json.JsonSerializer.Deserialize<OpenAIStreamChunk>(data,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (chunk == null)
                return;

            if (chunk.Choices != null && chunk.Choices.Count > 0)
            {
                var choice = chunk.Choices[0];
                
                // Buffer text content
                if (choice.Delta?.Content != null)
                {
                    if (buffer.TryAddChunk(choice.Delta.Content))
                    {
                        chunks.Add(choice.Delta.Content);
                    }
                }

                // Accumulate tool call fragments
                accumulator.AddDelta(choice.Delta?.ToolCalls?.ToArray());
            }
        }
        catch
        {
            // Ignore malformed chunks
        }
    }

    private static OpenAIStreamingChatCompletionRequest MapToOpenAIRequest(ModelRoute route, StreamingChatRequest request)
    {
        return new OpenAIStreamingChatCompletionRequest
        {
            Model = route.ProviderNativeModelId,
            Messages = request.BaseRequest.Messages.Select(m => new OpenAIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList(),
            Stream = true,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };
    }

    private string GetApiKeyFromConfiguration() => "";
}

// Infrastructure-level DTOs - must not leak to Application
internal sealed class OpenAIStreamingChatCompletionRequest
{
    public required string Model { get; init; }
    public required List<OpenAIChatMessage> Messages { get; init; }
    public bool Stream { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
}

internal sealed class OpenAIStreamChunk
{
    public List<OpenAIStreamChoice>? Choices { get; init; }
}

internal sealed class OpenAIStreamChoice
{
    public OpenAIStreamDelta? Delta { get; init; }
}

internal sealed class OpenAIStreamDelta
{
    public string? Content { get; init; }
    public List<OpenAIStreamDeltaToolCall>? ToolCalls { get; init; }
}
