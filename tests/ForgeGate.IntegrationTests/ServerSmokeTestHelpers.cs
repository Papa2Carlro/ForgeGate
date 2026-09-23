using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.IntegrationTests;

/// <summary>
/// Shared test doubles for ServerSmokeTests.
/// </summary>
internal sealed class DenyingAgentGuard : IAgentGuard
{
    public AgentGuardResult Evaluate(AgentAction action) =>
        AgentGuardResult.Success(PolicyDecision.Deny, ActionIntentKind.FileDelete, action.RawAction);
}

internal sealed class ApprovingAgentGuard : IAgentGuard
{
    public AgentGuardResult Evaluate(AgentAction action) =>
        AgentGuardResult.Success(PolicyDecision.RequireHumanApproval, ActionIntentKind.FileDelete, action.RawAction);
}

internal sealed class FailingTestHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
        {
            RequestMessage = request,
            Content = new StringContent("{\"error\":{\"message\":\"Bad Gateway\",\"type\":\"api_error\",\"code\":\"provider_unavailable\"}}")
        };
        response.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
        return Task.FromResult(response);
    }
}

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Action<HttpRequestMessage> _onRequest;
    private readonly Func<HttpResponseMessage> _responseFactory;
    public HttpResponseMessage? LastResponse { get; private set; }

    public TestHttpMessageHandler(
        Action<HttpRequestMessage> onRequest,
        Func<HttpResponseMessage> responseFactory)
    {
        _onRequest = onRequest;
        _responseFactory = responseFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _onRequest(request);
        var response = _responseFactory();
        response.RequestMessage = request;
        LastResponse = response;
        return Task.FromResult(response);
    }
}
