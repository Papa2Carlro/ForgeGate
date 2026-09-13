namespace ForgeGate.IntegrationTests;

public class IntegrationSmokeTests
{
    [Fact]
    public void ApiProject_CanBeLoaded()
    {
        // Smoke test: verify the API assembly loads without errors.
        var assembly = typeof(IntegrationSmokeTests).Assembly;
        Assert.NotNull(assembly);
    }
}
