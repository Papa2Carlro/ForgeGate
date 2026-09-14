using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing;
using ForgeGate.Domain.Providers;
using Microsoft.AspNetCore.Mvc;

namespace ForgeGate.Api.Controllers;

[ApiController]
[Route("v1")]
public sealed class ChatCompletionsController : ControllerBase
{
    private readonly ChatExecutionService _chatExecutionService;
    private readonly IRouteResolver _routeResolver;

    public ChatCompletionsController(
        ChatExecutionService chatExecutionService,
        IRouteResolver routeResolver)
    {
        _chatExecutionService = chatExecutionService;
        _routeResolver = routeResolver ?? throw new ArgumentNullException(nameof(routeResolver));
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

            // Resolve route using application-owned route resolver
            var routeResolutionOutcome = await _routeResolver.ResolveAsync(canonicalRequest, cancellationToken);
            
            // Handle route resolution failures
            if (!routeResolutionOutcome.IsSuccess)
            {
                var failure = routeResolutionOutcome.FailureValue!;
                return failure.Reason switch
                {
                    RouteResolutionReason.UnknownRequestedModel => BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"Model '{failure.Details?.Split('\'')[1].Split('\'')[0]}' not found",
                            Type = "invalid_request_error",
                            Param = "model",
                            Code = "model_not_found"
                        }
                    }),
                    RouteResolutionReason.NoConfiguredRoute => StatusCode(StatusCodes.Status503ServiceUnavailable, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "No routes configured",
                            Type = "api_error",
                            Param = null,
                            Code = "no_routes_configured"
                        }
                    }),
                    _ => StatusCode(StatusCodes.Status500InternalServerError, new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Route resolution failed",
                            Type = "api_error",
                            Param = null,
                            Code = "route_resolution_failed"
                        }
                    })
                };
            }

            // Execute chat completion with resolved route
            var resolvedRoute = routeResolutionOutcome.Route!;
            var outcome = await _chatExecutionService.ExecuteAsync(canonicalRequest, resolvedRoute, cancellationToken);

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
            var apiResponse = MapToApiResponse(outcome.Response!, resolvedRoute);

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

    private static CanonicalChatRequest MapToCanonical(OpenAIChatCompletionRequest request)
    {
        return new CanonicalChatRequest
        {
            RequestedModel = request.Model ?? string.Empty,
            Messages = request.Messages.Select(m => new CanonicalChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };
    }

    private static OpenAIChatCompletionResponse MapToApiResponse(CanonicalChatResponse canonicalResponse, ModelRoute route)
    {
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
                    FinishReason = "stop"
                }
            },
            Usage = null
        };
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
