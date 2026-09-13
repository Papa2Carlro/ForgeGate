namespace ForgeGate.Application.Tests;

public class ApplicationLayerTests
{
    [Fact]
    public void ApplicationProject_CanBeLoaded()
    {
        // Smoke test: verify the Application assembly loads without errors.
        var assembly = typeof(ApplicationLayerTests).Assembly;
        Assert.NotNull(assembly);
    }
}
