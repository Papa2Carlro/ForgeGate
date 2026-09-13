using ForgeGate.Application.Chat;

namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// Maps OpenAI-compatible HTTP errors to normalized ProviderFailure.
/// Owned by Infrastructure. Pure mapping function.
/// </summary>
public static class OpenAIProviderFailureMapper
{
    public static ProviderFailure Map(
        System.Net.HttpStatusCode statusCode,
        OpenAIError? error = null,
        System.Net.Http.Headers.HttpResponseHeaders? headers = null)
    {
        var code = (int)statusCode;
        var upstreamCode = error?.Code;
        var sanitizedMessage = SanitizeMessage(error?.Message);

        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => new ProviderFailure
            {
                Category = ProviderFailureCategory.AuthenticationFailed,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Credential,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            },

            System.Net.HttpStatusCode.Forbidden => new ProviderFailure
            {
                Category = ProviderFailureCategory.AuthorizationFailed,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Credential,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            },

            System.Net.HttpStatusCode.TooManyRequests => new ProviderFailure
            {
                Category = ProviderFailureCategory.RateLimited,
                Retryability = ProviderFailureRetryability.RetryAfterDelay,
                Scope = ProviderFailureScope.Provider,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage,
                RetryAfter = GetRetryAfter(headers)
            },

            System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.GatewayTimeout
            or System.Net.HttpStatusCode.BadGateway => new ProviderFailure
            {
                Category = ProviderFailureCategory.ProviderUnavailable,
                Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
                Scope = ProviderFailureScope.Provider,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            },

            System.Net.HttpStatusCode.RequestTimeout => new ProviderFailure
            {
                Category = ProviderFailureCategory.Timeout,
                Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
                Scope = ProviderFailureScope.Provider,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            },

            System.Net.HttpStatusCode.BadRequest => MapBadRequest(error, code, upstreamCode, sanitizedMessage),

            _ when code >= 400 && code < 500 => new ProviderFailure
            {
                Category = ProviderFailureCategory.InvalidRequest,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Request,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            },

            _ when code >= 500 => new ProviderFailure
            {
                Category = ProviderFailureCategory.ProviderUnavailable,
                Retryability = ProviderFailureRetryability.RetryViaAnotherRoute,
                Scope = ProviderFailureScope.Provider,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            },
            _ => new ProviderFailure
            {
                Category = ProviderFailureCategory.UnknownProviderFailure,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.Unknown,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            }
        };
    }

    private static ProviderFailure MapBadRequest(OpenAIError? error, int code, string? upstreamCode, string? sanitizedMessage)
    {
        if (error?.Code == "context_length_exceeded")
        {
            return new ProviderFailure
            {
                Category = ProviderFailureCategory.ContextExceeded,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.ModelRoute,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            };
        }

        if (error?.Code == "model_not_found")
        {
            return new ProviderFailure
            {
                Category = ProviderFailureCategory.InvalidModel,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.ModelRoute,
                UpstreamStatusCode = code,
                UpstreamCode = upstreamCode,
                SanitizedUpstreamMessage = sanitizedMessage
            };
        }

        return new ProviderFailure
        {
            Category = ProviderFailureCategory.InvalidRequest,
            Retryability = ProviderFailureRetryability.NotRetryable,
            Scope = ProviderFailureScope.Request,
            UpstreamStatusCode = code,
            UpstreamCode = upstreamCode,
            SanitizedUpstreamMessage = sanitizedMessage
        };
    }

    private static string? SanitizeMessage(string? message)
    {
        // Simple sanitization for demo - in reality would be more thorough
        if (string.IsNullOrWhiteSpace(message))
            return null;

        // Truncate long messages and remove potentially sensitive info
        if (message.Length > 200)
            return message.Substring(0, 200) + "...";

        return message;
    }

    private static TimeSpan? GetRetryAfter(System.Net.Http.Headers.HttpResponseHeaders? headers)
    {
        if (headers == null)
            return null;

        if (headers.TryGetValues("Retry-After", out var values) && values.FirstOrDefault() is { } value &&
            int.TryParse(value, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }
}