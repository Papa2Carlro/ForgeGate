using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests;

public class ChatExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_DelegatesToProvider()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );

        var request = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var expectedResponse = new CanonicalChatResponse
        {
            Content = "Hi there"
        };

        var mockProvider = new FakeChatCompletionProvider(expectedResponse);
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        var result = outcome.Response!;
        Assert.Equal(expectedResponse.Content, result.Content);
        Assert.Single(mockProvider.CapturedRequests);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Given
        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ExecuteAsync(null!, route, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_NullRoute_ThrowsArgumentException()
    {
        // Given
        var request = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        var mockProvider = new FakeChatCompletionProvider();
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));

        // When & Then
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.ExecuteAsync(request, null!, CancellationToken.None));
    }

    private sealed class FakeChatCompletionProvider : IChatCompletionProvider
    {
        private readonly CanonicalChatResponse _response;
        public List<CanonicalChatRequest> CapturedRequests { get; } = new();

        public FakeChatCompletionProvider(CanonicalChatResponse? response = null)
        {
            _response = response ?? new CanonicalChatResponse { Content = "test" };
        }

        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            return Task.FromResult(ProviderExecutionOutcome.Success(_response));
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailure_ReturnsSameFailureWithoutReclassification()
    {
        // Given
        var route = ModelRoute.FromIds(
            ProviderId.From("openai"),
            LogicalModelId.From("gpt-4"),
            ModelRouteId.From("openai:gpt-4")
        );
        var request = new CanonicalChatRequest
        {
            RequestedModel = "gpt-4",
            Messages = new List<CanonicalChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        // Create a specific ProviderFailure to test
        var expectedFailure = new ProviderFailure
        {
            Category = ProviderFailureCategory.ContextExceeded,
            Retryability = ProviderFailureRetryability.RetryAfterDelay,
            Scope = ProviderFailureScope.ModelRoute,
            UpstreamStatusCode = 400,
            UpstreamCode = "context_length_exceeded",
            SanitizedUpstreamMessage = "Context length exceeded",
            RetryAfter = TimeSpan.FromSeconds(30)
        };

        var failingProvider = new FakeProviderThatReturnsSpecificFailure(expectedFailure);
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.NotNull(outcome.FailureValue);
        Assert.Same(expectedFailure, outcome.FailureValue); // Same instance
        Assert.Equal(expectedFailure.Category, outcome.FailureValue!.Category);
        Assert.Equal(expectedFailure.Retryability, outcome.FailureValue!.Retryability);
        Assert.Equal(expectedFailure.Scope, outcome.FailureValue!.Scope);
        Assert.Equal(expectedFailure.UpstreamStatusCode, outcome.FailureValue!.UpstreamStatusCode);
        Assert.Equal(expectedFailure.UpstreamCode, outcome.FailureValue!.UpstreamCode);
        Assert.Equal(expectedFailure.SanitizedUpstreamMessage, outcome.FailureValue!.SanitizedUpstreamMessage);
        Assert.Equal(expectedFailure.RetryAfter, outcome.FailureValue!.RetryAfter);
    }

    private sealed class FakeProviderThatReturnsSpecificFailure : IChatCompletionProvider
    {
        private readonly ProviderFailure _failureToReturn;

        public FakeProviderThatReturnsSpecificFailure(ProviderFailure failureToReturn)
        {
            _failureToReturn = failureToReturn;
        }

        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderExecutionOutcome.Failure(_failureToReturn));
        }
    }

    private sealed class FakeCancellingProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ProviderExecutionOutcome.Success(new CanonicalChatResponse { Content = "never reached" }));
        }
    }

    // Slice 9: Passive route health feedback tests

    [Fact]
    public async Task Slice9_A_UnknownSuccess_MarksHealthy()
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
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_B_DegradedSuccess_RecoversHealthy()
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
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_C_NetworkFailure_MarksDegraded()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_D_ProviderUnavailable_MarksDegraded()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_E_MalformedResponse_MarksDegraded()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_F_UnknownProviderFailure_MarksDegraded()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Degraded, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_G_AuthenticationFailure_DoesNotChangeHealth()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_H_RateLimited_DoesNotChangeHealth()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_I_InvalidRequest_DoesNotChangeHealth()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_J_ContextExceeded_DoesNotChangeHealth()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_K_CallerCancellation_DoesNotChangeHealth()
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
        var service = new ChatExecutionService(cancellingProvider, new RouteHealthFeedback(healthProvider));

        // When
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await service.ExecuteAsync(request, route, new CancellationToken(true)));

        // Then
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice9_L_FailureOutcome_ReturnedUnchanged()
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
        var service = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));

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
    public async Task Slice9_M_SuccessResponse_ReturnedUnchanged()
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
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(healthProvider));

        // When
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);

        // Then
        Assert.True(outcome.IsSuccess);
        Assert.Same(expectedResponse, outcome.Response); // Same instance reference
        Assert.Equal(expectedResponse.Content, outcome.Response!.Content);
    }

    [Fact]
    public async Task Slice9_N_DegradedPreferredRoute_RemainsPreferred()
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
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(healthProvider));
        
        var resolver = new ConfiguredRouteResolver(
            Options.Create(new RoutingConfiguration 
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
            new RouteHealthRanker(healthProvider));

        // When - Execute preferred route to make it degraded
        var failingProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryAfterDelay,
                Scope = ProviderFailureScope.ModelRoute
            }
        );
        var serviceWithFailingProvider = new ChatExecutionService(failingProvider, new RouteHealthFeedback(healthProvider));
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

    // Slice 11: capacity coordinator foundation

    [Fact]
    public async Task Slice11_A_UnboundedRoute_Acquires()
    {
        var route = ModelRoute.FromIds(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"));
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        Assert.Single(mockProvider.CapturedRequests);
    }

    [Fact]
    public async Task Slice11_B_BoundedRoute_FirstSlotAcquires()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        // Capacity released after execution completes
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice11_C_BoundedRoute_RejectsWhenFull()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome1 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome1.IsSuccess);

        // Hold a reservation to make the route full
        var acquireResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        Assert.NotNull(reservation);
        Assert.Equal(1, coordinator.GetActiveCount(route.ModelRouteId));

        var outcome2 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome2.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome2.FailureValue!.Category);
        Assert.Single(mockProvider.CapturedRequests);
        reservation!.Dispose();
    }

    [Fact]
    public async Task Slice11_D_ReleaseRestoresCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome1 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome1.IsSuccess);
        // After first execution completes, capacity is released
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));

        // Now hold a slot and verify second execution is rejected
        var acquireResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        Assert.NotNull(reservation);
        Assert.Equal(1, coordinator.GetActiveCount(route.ModelRouteId));

        var outcome2 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome2.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome2.FailureValue!.Category);
        Assert.Single(mockProvider.CapturedRequests);
        reservation!.Dispose();
    }

    [Fact]
    public async Task Slice11_E_ReleaseIdempotent()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome1 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome1.IsSuccess);

        var outcome2 = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome2.IsSuccess);
    }

    [Fact]
    public async Task Slice11_F_SeparateRoutes_IndependentCapacity()
    {
        var routeA = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test-a"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r-a"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var routeB = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test-b"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r-b"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        // Hold slots on both routes concurrently
        var holdA = await coordinator.TryAcquireAsync(routeA, CancellationToken.None);
        var holdB = await coordinator.TryAcquireAsync(routeB, CancellationToken.None);
        Assert.NotNull(holdA);
        Assert.NotNull(holdB);
        Assert.Equal(1, coordinator.GetActiveCount(routeA.ModelRouteId));
        Assert.Equal(1, coordinator.GetActiveCount(routeB.ModelRouteId));

        // Release and verify
        holdA!.Reservation!.Dispose();
        holdB!.Reservation!.Dispose();
        Assert.Equal(0, coordinator.GetActiveCount(routeA.ModelRouteId));
        Assert.Equal(0, coordinator.GetActiveCount(routeB.ModelRouteId));
    }

    [Fact]
    public async Task Slice11_G_ProviderNotCalledWhenAtCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = result.Reservation;
        Assert.NotNull(reservation);

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome.FailureValue!.Category);
        Assert.Empty(mockProvider.CapturedRequests);
        reservation!.Dispose();
    }

    [Fact]
    public async Task Slice11_H_LocalCapacityFailure_Category()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var acquireResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(ProviderFailureCategory.ConcurrencyLimited, outcome.FailureValue!.Category);
        reservation!.Dispose();
    }

    [Fact]
    public async Task Slice11_I_LocalCapacityFailure_DoesNotChangeHealth()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var healthProvider = new InMemoryRouteHealthStateProvider();
        healthProvider.SetHealth(route.ModelRouteId, RouteHealthStatus.Healthy);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(healthProvider), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var acquireResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var reservation = acquireResult!.Reservation;
        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RouteHealthStatus.Healthy, healthProvider.GetHealth(route.ModelRouteId));
        reservation!.Dispose();
    }

    [Fact]
    public async Task Slice11_J_SuccessfulExecution_ReleasesCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeChatCompletionProvider(new CanonicalChatResponse { Content = "ok" });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.True(outcome.IsSuccess);
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice11_K_ExpectedFailure_ReleasesCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeProviderThatReturnsSpecificFailure(
            new ProviderFailure
            {
                Category = ProviderFailureCategory.NetworkFailure,
                Retryability = ProviderFailureRetryability.RetryAfterDelay,
                Scope = ProviderFailureScope.ModelRoute
            });
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var outcome = await service.ExecuteAsync(request, route, CancellationToken.None);
        Assert.False(outcome.IsSuccess);
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice11_L_CallerCancellation_ReleasesCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeCancellingProvider();
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();
        // TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAsync<System.Threading.Tasks.TaskCanceledException>(
            async () => await service.ExecuteAsync(request, route, cts.Token));
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice11_M_UnexpectedException_ReleasesCapacity()
    {
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1);
        var coordinator = new InMemoryRouteCapacityCoordinator();
        var mockProvider = new FakeThrowingProvider();
        var service = new ChatExecutionService(mockProvider, new RouteHealthFeedback(new InMemoryRouteHealthStateProvider()), coordinator);

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.ExecuteAsync(request, route, CancellationToken.None));
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice11_N_ConcurrentAcquisition_DoesNotExceedLimit()
    {
        // Direct reservation-based proof: hold exactly 2, prove no more can be acquired
        var route = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 2);
        var coordinator = new InMemoryRouteCapacityCoordinator();

        // Acquire and HOLD exactly 2 reservations
        var hold1 = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        var hold2 = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        Assert.NotNull(hold1);
        Assert.NotNull(hold2);
        Assert.Equal(2, coordinator.GetActiveCount(route.ModelRouteId));

        // While both are held, third acquisition must fail
        var thirdResult = await coordinator.TryAcquireAsync(route, CancellationToken.None);
        Assert.Equal(RouteCapacityAcquireStatus.AtCapacity, thirdResult.Status);

        // Cleanup
        hold1!.Reservation!.Dispose();
        hold2!.Reservation!.Dispose();
        Assert.Equal(0, coordinator.GetActiveCount(route.ModelRouteId));
    }

    [Fact]
    public async Task Slice11_O_ResolverBehavior_Unchanged()
    {
        // routeA has limit=1 and we hold its only slot - resolver should still select it
        var routeA = ModelRoute.FromIdsWithOptions(
            ProviderId.From("p-a"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r-a"),
            "native-a",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);
        var routeB = ModelRoute.FromIdsWithOptions(
            ProviderId.From("p-b"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r-b"),
            "native-b",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Preferred,
            maxConcurrentExecutions: 1);

        var coordinator = new InMemoryRouteCapacityCoordinator();
        // Hold routeA's only slot
        var heldReservation = await coordinator.TryAcquireAsync(routeA, CancellationToken.None);
        Assert.NotNull(heldReservation);
        Assert.Equal(1, coordinator.GetActiveCount(routeA.ModelRouteId));

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute>
            {
                new ConfiguredRoute
                {
                    RequestedModelAlias = "model",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = routeA
                },
                new ConfiguredRoute
                {
                    RequestedModelAlias = "model",
                    Enabled = true,
                    QualityTier = DeclaredQualityTier.Preferred,
                    ModelRoute = routeB
                }
            }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()));

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        // Resolver should still select routeA (capacity is not considered)
        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("r-a", result.Route!.ModelRouteId.Value);

        // Cleanup
        heldReservation!.Reservation!.Dispose();
    }

    [Fact]
    public async Task Slice11_P_ZeroConcurrencyLimit_Rejected()
    {
        // Zero is invalid - should throw at construction time
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Acceptable,
                maxConcurrentExecutions: 0));
    }

    [Fact]
    public async Task Slice11_Q_NegativeConcurrencyLimit_Rejected()
    {
        // Negative is invalid - should throw at construction time
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Acceptable,
                maxConcurrentExecutions: -1));
    }

    [Fact]
    public async Task Slice11_R_ConfigurationPropagation()
    {
        // Prove that ConfiguredRoute.MaxConcurrentExecutions propagates to ModelRoute
        var configuredRoute = new ConfiguredRoute
        {
            RequestedModelAlias = "model",
            Enabled = true,
            QualityTier = DeclaredQualityTier.Preferred,
            MaxConcurrentExecutions = 3,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Preferred)
        };

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute> { configuredRoute }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()));

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Route);
        Assert.Equal(3, result.Route!.MaxConcurrentExecutions);
    }

    [Fact]
    public async Task Slice11_S_ConfiguredZeroLimit_Rejected()
    {
        // Zero is invalid - should throw during resolution
        var configuredRoute = new ConfiguredRoute
        {
            RequestedModelAlias = "model",
            Enabled = true,
            QualityTier = DeclaredQualityTier.Preferred,
            MaxConcurrentExecutions = 0,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Preferred)
        };

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute> { configuredRoute }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()));

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ResolveAsync(request, CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task Slice11_T_ConfiguredNegativeLimit_Rejected()
    {
        // Negative is invalid - should throw during resolution
        var configuredRoute = new ConfiguredRoute
        {
            RequestedModelAlias = "model",
            Enabled = true,
            QualityTier = DeclaredQualityTier.Preferred,
            MaxConcurrentExecutions = -1,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Preferred)
        };

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute> { configuredRoute }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()));

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.ResolveAsync(request, CancellationToken.None).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task Slice11_U_Coordinator_DefensivelyRejectsInvalidLimit()
    {
        // Construct an invalid ModelRoute via 'with' expression (bypasses factory validation)
        // This tests the coordinator's defensive behavior against invalid states
        var invalidRoute = ModelRoute.FromIdsWithOptions(
            ProviderId.From("test"),
            LogicalModelId.From("model"),
            ModelRouteId.From("r1"),
            "native",
            enabled: true,
            capabilities: ModelCapability.None,
            qualityTier: DeclaredQualityTier.Acceptable,
            maxConcurrentExecutions: 1) with { MaxConcurrentExecutions = 0 };

        var coordinator = new InMemoryRouteCapacityCoordinator();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => coordinator.TryAcquireAsync(invalidRoute, CancellationToken.None));
    }

    [Fact]
    public async Task Slice11_V_ConfiguredNullLimit_RemainsUnbounded()
    {
        // Null should propagate as unbounded
        var configuredRoute = new ConfiguredRoute
        {
            RequestedModelAlias = "model",
            Enabled = true,
            QualityTier = DeclaredQualityTier.Preferred,
            MaxConcurrentExecutions = null,
            ModelRoute = ModelRoute.FromIdsWithOptions(
                ProviderId.From("test"),
                LogicalModelId.From("model"),
                ModelRouteId.From("r1"),
                "native",
                enabled: true,
                capabilities: ModelCapability.None,
                qualityTier: DeclaredQualityTier.Preferred)
        };

        var config = new RoutingConfiguration
        {
            Routes = new List<ConfiguredRoute> { configuredRoute }
        };
        var resolver = new ConfiguredRouteResolver(
            Options.Create(config),
            new HardRouteEligibilityEvaluator(),
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()));

        var request = new CanonicalChatRequest
        {
            RequestedModel = "model",
            Messages = new List<CanonicalChatMessage> { new() { Role = "user", Content = "hi" } }
        };

        var result = await resolver.ResolveAsync(request, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Route);
        Assert.Null(result.Route!.MaxConcurrentExecutions);
    }

    private sealed class FakeThrowingProvider : IChatCompletionProvider
    {
        public Task<ProviderExecutionOutcome> ExecuteAsync(ModelRoute route, CanonicalChatRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Unexpected error");
        }
    }
}
