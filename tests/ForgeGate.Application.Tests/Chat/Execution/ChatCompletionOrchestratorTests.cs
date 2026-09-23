using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Execution;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Resolution;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Execution;

/// <summary>
/// Placeholder keeping namespace and assembly metadata.
/// Actual tests have been moved to:
///   - ChatCompletionOrchestratorFailoverTests (failover, exhaustion, tier locking, route exclusion, capacity)
///   - ChatCompletionOrchestratorCoreTests (core orchestration, exceptions, edge cases)
///   - ChatCompletionOrchestratorTestHelpers (shared helpers and test doubles)
/// </summary>
public partial class ChatCompletionOrchestratorTests
{
}
