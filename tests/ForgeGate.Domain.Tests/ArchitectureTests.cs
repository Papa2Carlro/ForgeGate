using System.Reflection;

namespace ForgeGate.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceApplicationOrInfrastructure()
    {
        var domainAssembly = typeof(ForgeGate.Domain.ModelAlias).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();
        var referencedNames = referencedAssemblies.Select(a => a.Name).ToList();

        Assert.DoesNotContain("ForgeGate.Application", referencedNames);
        Assert.DoesNotContain("ForgeGate.Infrastructure", referencedNames);
        Assert.DoesNotContain("ForgeGate.Api", referencedNames);
    }
}
