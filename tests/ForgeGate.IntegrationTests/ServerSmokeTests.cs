using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Streaming;
using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.Providers;
using ForgeGate.Domain.AgentGuard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Infrastructure.Routing;
using ForgeGate.Infrastructure.Providers.OpenAICompatible;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Microsoft.Extensions.Configuration.Memory;

namespace ForgeGate.IntegrationTests;

/// <summary>
/// Placeholder keeping namespace and assembly metadata.
/// Actual tests have been moved to:
///   - ChatCompletionsSuccessSmokeTests (success paths, tool calls, real provider)
///   - ChatCompletionsAgentGuardSmokeTests (agent guard deny/approve)
///   - ChatCompletionsProviderFailureSmokeTests (503 failover exhaustion)
///   - ChatCompletionsInputValidationSmokeTests (400 input validation)
///   - ServerSmokeTestHelpers (shared test doubles)
/// </summary>
public partial class ServerSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServerSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
}
