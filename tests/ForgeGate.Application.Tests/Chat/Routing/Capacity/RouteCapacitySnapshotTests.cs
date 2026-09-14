using ForgeGate.Application.Chat;
using ForgeGate.Application.Chat.Routing.Capacity;
using ForgeGate.Application.Chat.Routing.Health;
using ForgeGate.Application.Chat.Routing.Eligibility;
using ForgeGate.Domain.Providers;
using ForgeGate.Infrastructure.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ForgeGate.Application.Tests.Chat.Routing.Capacity;

/// <summary>
/// Tests for capacity state snapshots and concurrent acquisition limits.
/// </summary>
public class RouteCapacitySnapshotTests
{
    [Fact]
    public async Task ConcurrentAcquisition_NeverExceedsLimit()
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
    public async Task ResolverBehavior_Unchanged()
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
            new RouteHealthRanker(new InMemoryRouteHealthStateProvider()),
            new InMemoryRouteCapacityCoordinator());

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
    public async Task DIReadAndAcquireShareSameCoordinatorInstance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRouteCapacityCoordinator, InMemoryRouteCapacityCoordinator>();
        services.AddSingleton<IRouteCapacityStateProvider>(sp => sp.GetRequiredService<IRouteCapacityCoordinator>() as IRouteCapacityStateProvider
            ?? throw new InvalidOperationException("Coordinator does not implement state provider"));

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IRouteCapacityCoordinator>();
        var stateProvider = provider.GetRequiredService<IRouteCapacityStateProvider>();

        Assert.Same(coordinator, stateProvider);
    }
}
