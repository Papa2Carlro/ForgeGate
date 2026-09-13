using ForgeGate.Application.Chat;
using ForgeGate.Infrastructure.Providers.OpenAICompatible;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ForgeGate.Application.Tests;

public class ProviderFailureTests
{
    [Fact]
    public void Mapper_401_AuthFailed()
    {
        var failure = OpenAIProviderFailureMapper.Map(System.Net.HttpStatusCode.Unauthorized);
        
        Assert.Equal(ProviderFailureCategory.AuthenticationFailed, failure.Category);
        Assert.Equal(ProviderFailureRetryability.NotRetryable, failure.Retryability);
        Assert.Equal(ProviderFailureScope.Credential, failure.Scope);
        Assert.Equal(401, failure.UpstreamStatusCode);
    }

    [Fact]
    public void Mapper_429_RateLimited_WithRetryAfter()
    {
        var httpResponse = new System.Net.Http.HttpResponseMessage();
        var headers = httpResponse.Headers;
        headers.TryAddWithoutValidation("Retry-After", "30");
        
        var failure = OpenAIProviderFailureMapper.Map(
            System.Net.HttpStatusCode.TooManyRequests,
            headers: headers);
        
        Assert.Equal(ProviderFailureCategory.RateLimited, failure.Category);
        Assert.Equal(ProviderFailureRetryability.RetryAfterDelay, failure.Retryability);
        Assert.Equal(ProviderFailureScope.Provider, failure.Scope);
        Assert.NotNull(failure.RetryAfter);
    }

    [Fact]
    public void Mapper_400_ContextExceeded()
    {
        var error = new OpenAIError
        {
            Code = "context_length_exceeded",
            Message = "Context length exceeded"
        };
        
        var failure = OpenAIProviderFailureMapper.Map(System.Net.HttpStatusCode.BadRequest, error);
        
        Assert.Equal(ProviderFailureCategory.ContextExceeded, failure.Category);
        Assert.Equal(ProviderFailureRetryability.NotRetryable, failure.Retryability);
        Assert.Equal(ProviderFailureScope.ModelRoute, failure.Scope);
    }

    [Fact]
    public void Mapper_400_InvalidModel()
    {
        var error = new OpenAIError
        {
            Code = "model_not_found",
            Message = "Model not found"
        };
        
        var failure = OpenAIProviderFailureMapper.Map(System.Net.HttpStatusCode.BadRequest, error);
        
        Assert.Equal(ProviderFailureCategory.InvalidModel, failure.Category);
        Assert.Equal(ProviderFailureScope.ModelRoute, failure.Scope);
    }

    [Fact]
    public void Mapper_503_ProviderUnavailable()
    {
        var failure = OpenAIProviderFailureMapper.Map(System.Net.HttpStatusCode.ServiceUnavailable);
        
        Assert.Equal(ProviderFailureCategory.ProviderUnavailable, failure.Category);
        Assert.Equal(ProviderFailureRetryability.RetryViaAnotherRoute, failure.Retryability);
        Assert.Equal(ProviderFailureScope.Provider, failure.Scope);
    }

    [Fact]
    public void Mapper_500_DefaultsToProviderUnavailable()
    {
        var failure = OpenAIProviderFailureMapper.Map(System.Net.HttpStatusCode.InternalServerError);
        
        Assert.Equal(ProviderFailureCategory.ProviderUnavailable, failure.Category);
        Assert.Equal(ProviderFailureRetryability.RetryViaAnotherRoute, failure.Retryability);
    }

    [Fact]
    public void Mapper_SanitizesLongMessage()
    {
        var longMessage = new string('x', 500);
        var error = new OpenAIError { Message = longMessage };
        
        var failure = OpenAIProviderFailureMapper.Map(System.Net.HttpStatusCode.BadRequest, error);
        
        Assert.NotNull(failure.SanitizedUpstreamMessage);
        Assert.True(failure.SanitizedUpstreamMessage!.Length <= 203);
        Assert.EndsWith("...", failure.SanitizedUpstreamMessage);
    }

    [Fact]
    public void Mapper_403_AuthFailed()
    {
        var failure = OpenAIProviderFailureMapper.Map(System.Net.HttpStatusCode.Forbidden);
        
        Assert.Equal(ProviderFailureCategory.AuthorizationFailed, failure.Category);
        Assert.Equal(ProviderFailureRetryability.NotRetryable, failure.Retryability);
        Assert.Equal(ProviderFailureScope.Credential, failure.Scope);
        Assert.Equal(403, failure.UpstreamStatusCode);
    }

    [Fact]
    public void Mapper_400_GenericInvalidRequest()
    {
        var error = new OpenAIError
        {
            Code = "some_other_error",
            Message = "Some other error"
        };
        
        var failure = OpenAIProviderFailureMapper.Map(System.Net.HttpStatusCode.BadRequest, error);
        
        Assert.Equal(ProviderFailureCategory.InvalidRequest, failure.Category);
        Assert.Equal(ProviderFailureRetryability.NotRetryable, failure.Retryability);
        Assert.Equal(ProviderFailureScope.Request, failure.Scope);
    }
}
