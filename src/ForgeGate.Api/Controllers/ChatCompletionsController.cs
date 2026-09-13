using ForgeGate.Application.Chat;
using ForgeGate.Domain.Chat;
using Microsoft.AspNetCore.Mvc;

namespace ForgeGate.Api.Controllers;

[ApiController]
[Route("v1")]
public sealed class ChatCompletionsController : ControllerBase
{
    private readonly ChatExecutionService _chatExecutionService;

    public ChatCompletionsController(ChatExecutionService chatExecutionService)
    {
        _chatExecutionService = chatExecutionService;
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

            // Execute chat completion
            var canonicalResponse = await _chatExecutionService.ExecuteAsync(canonicalRequest, cancellationToken);

            // Map canonical response to API response
            var apiResponse = MapToApiResponse(canonicalResponse);

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
        catch (ForgeGate.Infrastructure.Providers.OpenAICompatible.ProviderRequestFailedException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = "Provider request failed",
                    Type = "api_error",
                    Param = null,
                    Code = "provider_request_failed"
                }
            });
        }
        catch (ForgeGate.Infrastructure.Providers.OpenAICompatible.ProviderResponseUnusableException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new OpenAIErrorResponse
            {
                Error = new OpenAIError
                {
                    Message = "Provider response unusable",
                    Type = "api_error",
                    Param = null,
                    Code = "provider_response_unusable"
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
            Model = request.Model,
            Messages = request.Messages.Select(m => new CanonicalChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };
    }

    private static OpenAIChatCompletionResponse MapToApiResponse(CanonicalChatResponse canonicalResponse)
    {
        return new OpenAIChatCompletionResponse
        {
            Id = Guid.NewGuid().ToString("N"),
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = canonicalResponse.Model,
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
}

/// <summary>
/// OpenAI-compatible error response DTO.
/// </summary>
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