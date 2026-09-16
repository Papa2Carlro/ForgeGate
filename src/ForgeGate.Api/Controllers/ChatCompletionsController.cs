using System.Text;
using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Domain.Providers;
using Microsoft.AspNetCore.Mvc;

namespace ForgeGate.Api.Controllers;

[ApiController]
[Route("v1")]
public sealed class ChatCompletionsController : ControllerBase
{
    private readonly IChatCompletionOrchestrator _orchestrator;
    private readonly IStreamingChatCompletionOrchestrator _streamingOrchestrator;

    public ChatCompletionsController(
        IChatCompletionOrchestrator orchestrator,
        IStreamingChatCompletionOrchestrator streamingOrchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _streamingOrchestrator = streamingOrchestrator ?? throw new ArgumentNullException(nameof(streamingOrchestrator));
    }

    /// <summary>
    /// Creates a chat completion.
    /// OpenAI-compatible endpoint for non-streaming chat completions.
    /// </summary>
    [HttpPost("chat/completions")]
    [ProducesResponseType(typeof(OpenAIChatCompletionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateChatCompletion(
        [FromBody] OpenAIChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate API request at API boundary
            if (request == null)
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Request body is required",
                        Type = "invalid_request_error",
                        Param = null,
                        Code = null
                    }
                });

            if (string.IsNullOrWhiteSpace(request.Model))
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Model is required",
                        Type = "invalid_request_error",
                        Param = "model",
                        Code = null
                    }
                });

            if (request.Messages == null || request.Messages.Count == 0)
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Messages are required",
                        Type = "invalid_request_error",
                        Param = "messages",
                        Code = null
                    }
                });

            // Validate message roles
            foreach (var message in request.Messages)
            {
                if (string.IsNullOrWhiteSpace(message.Role))
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Message role is required",
                            Type = "invalid_request_error",
                            Param = "messages",
                            Code = null
                        }
                    });

                var validRoles = new[] { "system", "user", "assistant", "tool" };
                if (!validRoles.Contains(message.Role.ToLowerInvariant()))
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"Invalid message role: {message.Role}",
                            Type = "invalid_request_error",
                            Param = "messages",
                            Code = null
                        }
                    });
            }

            // Map API request to canonical model
            var canonicalRequest = MapToCanonical(request);

            // Branch on stream flag
            if (request.Stream == true)
            {
                return await ExecuteStreamingAsync(canonicalRequest, cancellationToken);
            }

            // Execute through non-streaming orchestrator (includes routing + failover)
            var outcome = await _orchestrator.ExecuteAsync(canonicalRequest, cancellationToken);

            // Map outcome to API response
            if (!outcome.IsSuccess)
            {
                var failure = outcome.FailureValue!;
                int statusCode = MapFailureToStatusCode(failure);
                string errorType = MapFailureToErrorType(failure.Category);
                string errorCode = MapFailureToErrorCode(failure.Category);

                return StatusCode(statusCode, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = failure.SanitizedUpstreamMessage ?? "Provider error",
                        Type = errorType,
                        Param = null,
                        Code = errorCode
                    }
                });
            }

            // Map canonical response to API response
            var apiResponse = MapToApiResponse(outcome.Response!, outcome.SelectedRoute!);

            return Ok(apiResponse);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = ex.Message,
                    Type = "invalid_request_error",
                    Param = null,
                    Code = null
                }
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = "Request cancelled",
                    Type = "api_error",
                    Param = null,
                    Code = "request_cancelled"
                }
            });
        }
        catch (Exception)
        {
            // Log the exception (would use logging in real implementation)
            return StatusCode(StatusCodes.Status500InternalServerError, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = "Internal server error",
                    Type = "api_error",
                    Param = null,
                    Code = null
                }
            });
        }
    }

    /// <summary>
    /// Executes streaming chat completion through the streaming orchestrator and emits SSE response.
    /// </summary>
    private async Task<IActionResult> ExecuteStreamingAsync(CanonicalChatRequest canonicalRequest, CancellationToken cancellationToken)
    {
        var buffer = new StreamingBuffer();
        var streamingRequest = new StreamingChatRequest
        {
            BaseRequest = canonicalRequest,
            Stream = true
        };

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            var outcome = await _streamingOrchestrator.ExecuteStreamingAsync(
                streamingRequest,
                buffer,
                cancellationToken);

            if (!outcome.IsSuccess)
            {
                var failure = outcome.FailureValue!;
                int statusCode = MapFailureToStatusCode(failure);
                string errorType = MapFailureToErrorType(failure.Category);
                string errorCode = MapFailureToErrorCode(failure.Category);

                return StatusCode(statusCode, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = failure.SanitizedUpstreamMessage ?? "Streaming provider error",
                        Type = errorType,
                        Param = null,
                        Code = errorCode
                    }
                });
            }

            // Emit SSE response with buffered chunks
            if (outcome.SelectedRoute != null)
            {
                var model = outcome.SelectedRoute.LogicalModelId.Value;
                long created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string id = Guid.NewGuid().ToString("N");

                // Emit text content chunks
                foreach (var chunk in outcome.BufferedChunks)
                {
                    var sseEvent = $"id: {Guid.NewGuid():N}\nevent: message\ndata: {{\"id\":\"{id}\",\"object\":\"chat.completion.chunk\",\"created\":{created},\"model\":\"{model}\",\"choices\":[{{\"index\":0,\"delta\":{{\"content\":\"{EscapeSseData(chunk)}\"}}}}]}}\n\n";
                    await Response.WriteAsync(sseEvent, System.Text.Encoding.UTF8, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // Emit tool call deltas if present
                if (outcome.ToolCalls.Count > 0)
                {
                    foreach (var toolCall in outcome.ToolCalls)
                    {
                        var toolCallEvent = BuildToolCallSseEvent(id, created, model, toolCall);
                        await Response.WriteAsync(toolCallEvent, System.Text.Encoding.UTF8, cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                    }
                }

                // Send final done event
                var finishReason = outcome.ToolCalls.Count > 0 ? "tool_calls" : "stop";
                var finalEvent = $"id: {Guid.NewGuid():N}\nevent: message\ndata: {{\"id\":\"{id}\",\"object\":\"chat.completion.chunk\",\"created\":{created},\"model\":\"{model}\",\"choices\":[{{\"index\":0,\"delta\":{{}},\"finish_reason\":\"{finishReason}\"}}],\"usage\":null}}\n\n";
                await Response.WriteAsync(finalEvent, System.Text.Encoding.UTF8, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            return Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = "Internal server error",
                    Type = "api_error",
                    Param = null,
                    Code = null
                }
            });
        }
    }

    private static string EscapeSseData(string data)
    {
        return data.Replace("\\", "\\\\")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\"", "\\\"");
    }

    private static CanonicalChatRequest MapToCanonical(OpenAIChatCompletionRequest request)
    {
        var toolRequirement = ToolRequirement.None;
        if (request.Tools != null && request.Tools.Any())
        {
            if (request.ToolChoice != null && (request.ToolChoice == "none" || request.ToolChoice == ""))
                toolRequirement = ToolRequirement.None;
            else if (request.ToolChoice != null && request.ToolChoice.Contains("function"))
                toolRequirement = ToolRequirement.Required;
            else
                toolRequirement = ToolRequirement.Optional;
        }

        return new CanonicalChatRequest
        {
            RequestedModel = request.Model ?? string.Empty,
            Messages = request.Messages.Select(m => new CanonicalChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList(),
            ToolRequirement = toolRequirement
        };
    }

    private static OpenAIChatCompletionResponse MapToApiResponse(CanonicalChatResponse canonicalResponse, ModelRoute route)
    {
        var finishReason = canonicalResponse.ToolCalls.Count > 0 ? "tool_calls" : "stop";
        
        return new OpenAIChatCompletionResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = route.LogicalModelId.Value,
            Choices = new List<OpenAIChatCompletionChoice>
            {
                new OpenAIChatCompletionChoice
                {
                    Index = 0,
                    Message = new OpenAIChatMessage
                    {
                        Role = "assistant",
                        Content = canonicalResponse.Content
                    },
                    FinishReason = finishReason,
                    ToolCalls = canonicalResponse.ToolCalls.Count > 0
                        ? canonicalResponse.ToolCalls.Select(MapToApiToolCall).ToList()
                        : null
                }
            },
            Usage = null
        };
    }

    private static OpenAIChatCompletionToolCall MapToApiToolCall(ToolCallInvocation invocation)
    {
        return new OpenAIChatCompletionToolCall
        {
            Id = invocation.Id,
            Function = new OpenAIChatCompletionToolCallFunction
            {
                Name = invocation.Name,
                Arguments = invocation.Arguments
            }
        };
    }

    /// <summary>
    /// Builds an SSE event for a tool call in streaming response.
    /// </summary>
    public static string BuildToolCallSseEvent(string id, long created, string model, ToolCallInvocation toolCall)
    {
        var toolCallJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            index = toolCall.Index,
            id = toolCall.Id,
            type = "function",
            @function = new
            {
                name = toolCall.Name,
                arguments = toolCall.Arguments
            }
        });

        return $"id: {Guid.NewGuid():N}\nevent: message\ndata: {{\"id\":\"{id}\",\"object\":\"chat.completion.chunk\",\"created\":{created},\"model\":\"{model}\",\"choices\":[{{\"index\":0,\"delta\":{{\"tool_calls\":[{toolCallJson}]}}}}]}}\n\n";
    }

    private static int MapFailureToStatusCode(ProviderFailure failure)
    {
        switch (failure.Category)
        {
            case ProviderFailureCategory.AuthenticationFailed:
                return StatusCodes.Status401Unauthorized;
            case ProviderFailureCategory.AuthorizationFailed:
                return StatusCodes.Status403Forbidden;
            case ProviderFailureCategory.RateLimited:
                return StatusCodes.Status429TooManyRequests;
            case ProviderFailureCategory.QuotaExhausted:
                return StatusCodes.Status429TooManyRequests;
            case ProviderFailureCategory.ContextExceeded:
                return StatusCodes.Status400BadRequest;
            case ProviderFailureCategory.InvalidModel:
                return StatusCodes.Status400BadRequest;
            case ProviderFailureCategory.InvalidRequest:
                return StatusCodes.Status400BadRequest;
            case ProviderFailureCategory.Timeout:
                return StatusCodes.Status504GatewayTimeout;
            case ProviderFailureCategory.ProviderUnavailable:
                return StatusCodes.Status502BadGateway;
            case ProviderFailureCategory.NetworkFailure:
                return StatusCodes.Status502BadGateway;
            case ProviderFailureCategory.MalformedResponse:
                return StatusCodes.Status502BadGateway;
            default:
                return StatusCodes.Status502BadGateway;
        }
    }

    private static string MapFailureToErrorType(ProviderFailureCategory category)
    {
        return category switch
        {
            ProviderFailureCategory.AuthenticationFailed => "authentication_error",
            ProviderFailureCategory.AuthorizationFailed => "authorization_error",
            ProviderFailureCategory.RateLimited => "rate_limit_error",
            ProviderFailureCategory.QuotaExhausted => "rate_limit_error",
            ProviderFailureCategory.ContextExceeded => "invalid_request_error",
            ProviderFailureCategory.InvalidModel => "invalid_request_error",
            ProviderFailureCategory.InvalidRequest => "invalid_request_error",
            ProviderFailureCategory.Timeout => "api_error",
            ProviderFailureCategory.ProviderUnavailable => "api_error",
            ProviderFailureCategory.NetworkFailure => "api_error",
            ProviderFailureCategory.MalformedResponse => "api_error",
            _ => "api_error"
        };
    }

    private static string MapFailureToErrorCode(ProviderFailureCategory category)
    {
        return category switch
        {
            ProviderFailureCategory.AuthenticationFailed => "authentication_failed",
            ProviderFailureCategory.AuthorizationFailed => "authorization_failed",
            ProviderFailureCategory.RateLimited => "rate_limited",
            ProviderFailureCategory.QuotaExhausted => "quota_exhausted",
            ProviderFailureCategory.ContextExceeded => "context_length_exceeded",
            ProviderFailureCategory.InvalidModel => "invalid_model",
            ProviderFailureCategory.InvalidRequest => "invalid_request",
            ProviderFailureCategory.Timeout => "timeout",
            ProviderFailureCategory.ProviderUnavailable => "provider_unavailable",
            ProviderFailureCategory.NetworkFailure => "network_failure",
            ProviderFailureCategory.MalformedResponse => "malformed_response",
            _ => "unknown_error"
        };
    }
}

public sealed class OpenAIErrorResponse
{
    public required OpenAIError Error { get; init; }
}

public sealed class OpenAIError
{
    public required string Message { get; init; }
    public required string Type { get; init; }
    public string? Param { get; init; }
    public string? Code { get; init; }
}
