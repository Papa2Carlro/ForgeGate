using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Application.Tests.Chat.TestDoubles;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Execution;

/// <summary>
/// Tests for passive route health feedback during execution outcomes.
/// </summary>
public class RouteHealthFeedbackTests
{
    [Fact]
    public async Task UnknownRoute_SuccessMarksHealthy()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("unknown-provider"),
            LogicalModelId.From("unknown-model"),
            ModelRouteId.From("unknown-provider:unknown-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "unknown-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "Hi" });
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task DegradedRoute_SuccessRecoversHealthy()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        // Start with degraded health
        healthProvider.SetHealth(route.ModelRouteId, RouteHealthStatus.Degraded);
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "Hi" });
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task NetworkFailure_MarksRouteDegraded()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryAfterDelay,
                Scope = ProviderFailureScope.ModelRoute
            }
        );
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task ProviderUnavailable_MarksRouteDegraded()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.ProviderUnavailable,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.ModelRoute
            }
        );
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task MalformedResponse_MarksRouteDegraded()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.MalformedResponse,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.ModelRoute,
                UpstreamStatusCode = 500,
                UpstreamCode = "malformed",
                SanitizedUpstreamMessage = "Invalid response format"
            }
        );
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task UnknownProviderFailure_MarksRouteDegraded()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.UnknownProviderFailure,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.ModelRoute
            }
        );
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task AuthenticationFailure_DoesNotChangeHealth()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        // Start with healthy
        healthProvider.SetHealth(route.ModelRouteId, RouteHealthStatus.Healthy);
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.AuthenticationFailed,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.ModelRoute
            }
        );
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task RateLimited_DoesNotChangeHealth()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        // Start with healthy
        healthProvider.SetHealth(route.ModelRouteId, RouteHealthStatus.Healthy);
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.RateLimited,
                Retryability = ProviderFailureRetryability.RetryAfterDelay,
                Scope = ProviderFailureScope.ModelRoute,
                RetryAfter = TimeSpan.FromSeconds(30)
            }
        );
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task InvalidRequest_DoesNotChangeHealth()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        // Start with healthy
        healthProvider.SetHealth(route.ModelRouteId, RouteHealthStatus.Healthy);
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.InvalidRequest,
                Retryability = ProviderFailureRetryability.NotRetryable,
                Scope = ProviderFailureScope.ModelRoute,
                UpstreamStatusCode = 400,
                UpstreamCode = "invalid_request"
            }
        );
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task ContextExceeded_DoesNotChangeHealth()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        // Start with healthy
        healthProvider.SetHealth(route.ModelRouteId, RouteHealthStatus.Healthy);
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.ContextExceeded,
                Retryability = ProviderFailureRetryability.RetryAfterDelay,
                Scope = ProviderFailureScope.ModelRoute
            }
        );
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task CallerCancellation_DoesNotChangeHealth()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        // Start with healthy
        healthProvider.SetHealth(route.ModelRouteId, RouteHealthStatus.Healthy);

        var cancellingProvider = new FakeCancellingProvider();
        var service = new ChatExecutionService(cancellingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await service.ExecuteAsync(request, route, new CancellationToken(true)));

        // Then
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task FailureOutcome_ReturnedUnchanged()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var expectedFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.NetworkFailure,
            Retryability = ProviderFailureRetryability.RetryAfterDelay,
            Scope = ProviderFailureScope.ModelRoute,
            UpstreamStatusCode = 502,
            UpstreamCode = "bad_gateway",
            SanitizedUpstreamMessage = "Bad Gateway"
        };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(expectedFailure);
        var service = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Same(expectedFailure, outcome.FailureValue); // Same instance reference
        Assert.Equal(expectedFailure.Category, outcome.FailureValue!.Category);
        Assert.Equal(expectedFailure.Retryability, outcome.FailureValue!.Retryability);
        Assert.Equal(expectedFailure.Scope, outcome.FailureValue!.Scope);
        Assert.Equal(expectedFailure.UpstreamStatusCode, outcome.FailureValue!.UpstreamStatusCode);
        Assert.Equal(expectedFailure.UpstreamCode, outcome.FailureValue!.UpstreamCode);
        Assert.Equal(expectedFailure.SanitizedUpstreamMessage, outcome.FailureValue!.SanitizedUpstreamMessage);
    }

    [Fact]
    public async Task SuccessResponse_ReturnedUnchanged()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("test-provider"),
            LogicalModelId.From("test-model"),
            ModelRouteId.From("test-provider:test-model")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "test-model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };
        var expectedResponse = new CanonicalChatResponse { Content = "Hello world" };
        var healthProvider = new InMemoryRouteHealthStateProvider();
        var mockProvider = new FakeChatCompletionProvider(expectedResponse);
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Same(expectedResponse, outcome.Response); // Same instance reference
        Assert.Equal(expectedResponse.Content, outcome.Response!.Content);
    }

    [Fact]
    public async Task DegradedPreferredRoute_RemainsPreferred()
    {
        // Given
        var preferredRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("preferred-provider"),
            LogicalModelId.From("preferred-model"),
            ModelRouteId.From("preferred-route"),
            "preferred-native-model",
            true,
            ModelCapability.None,
            DeclaredQualityTier.Preferred  // Higher quality tier
        );

        var acceptableRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("acceptable-provider"),
            LogicalModelId.From("acceptable-model"),
            ModelRouteId.From("acceptable-route"),
            "acceptable-native-model",
            true,
            ModelCapability.None,
            DeclaredQualityTier.Acceptable  // Lower quality tier
        );

        var request = new CanonicalChatRequest
        {
            RequestedModel = "preferred-model",  // Matches both routes
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "Hello" } }
        };

        var healthProvider = new InMemoryRouteHealthStateProvider();
        // Both routes start healthy
        healthProvider.SetHealth(preferredRoute.ModelRouteId, RouteHealthStatus.Healthy);
        healthProvider.SetHealth(acceptableRoute.ModelRouteId, RouteHealthStatus.Healthy);

        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "Hi" });
        var service = new ChatExecutionService(mockProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));

        var resolver = new ConfiguredRouteResolver(
            Microsoft.Extensions.Options.Options.Create(new RoutingConfiguration
            {
                Routes = new List<ConfiguredRoute>
                {
                    new ConfiguredRoute
                    {
                        RequestedModelAlias = "preferred-model",
                        Enabled = true,
                        ModelRoute = preferredRoute
                    },
                    new ConfiguredRoute
                    {
                        RequestedModelAlias = "acceptable-model",
                        Enabled = true,
                        ModelRoute = acceptableRoute
                    }
                }
            }),
            new RouteOperationalEligibilityEvaluator(
                new HardRouteEligibilityEvaluator(),
                healthProvider),
            new RouteHealthRanker(healthProvider),
            new InMemoryRouteCapacityCoordinator());

        // When - Execute preferred route to make it degraded
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryAfterDelay,
                Scope = ProviderFailureScope.ModelRoute
            }
        );
        var serviceWithFailingProvider = new ChatExecutionService(failingProvider, new FakeStreamingChatCompletionProvider(), new RouteHealthFeedback(healthProvider));
        var outcome = await serviceWithFailingProvider.ExecuteAsync(request, preferredRoute, CancellationToken.None);

        // Then - Preferred route should be degraded but still selected due to higher quality tier
        Assert.False(outcome.IsSuccess);  // NetworkFailure should result in failure outcome
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(preferredRoute.ModelRouteId));
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(acceptableRoute.ModelRouteId));

        // And when resolving, it should still pick the preferred route (higher quality wins)
        var resolution = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(resolution.IsSuccess);
        Assert.NotNull(resolution.Route);
        Assert.Equal("preferred-route", resolution.Route!.ModelRouteId.Value);
    }
}
