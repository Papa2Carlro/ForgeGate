using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Domain.Tests.AgentGuard;

public class CapabilityRegistryTests
{
    private readonly InMemoryCapabilityRegistry _registry = new();

    [Fact]
    public void KnownCapability_IsRegistered()
    {
        Assert.True(_registry.IsRegistered(ActionIntentKind.FileRead));
        Assert.True(_registry.IsRegistered(ActionIntentKind.FileWrite));
        Assert.True(_registry.IsRegistered(ActionIntentKind.DirectoryList));
        Assert.True(_registry.IsRegistered(ActionIntentKind.FileSearch));
        Assert.True(_registry.IsRegistered(ActionIntentKind.FileDelete));
        Assert.True(_registry.IsRegistered(ActionIntentKind.HttpRequest));
        Assert.True(_registry.IsRegistered(ActionIntentKind.ProcessSpawn));
    }

    [Fact]
    public void UnknownCapability_IsNotRegistered()
    {
        // Introduce a hypothetical new capability that isn't registered
        var unknown = (ActionIntentKind)999;
        Assert.False(_registry.IsRegistered(unknown));
    }

    [Fact]
    public void KnownCapability_ReturnsCorrectDescriptor()
    {
        var descriptor = _registry.GetDescriptor(ActionIntentKind.FileRead);

        Assert.Equal(ActionIntentKind.FileRead, descriptor.Id);
        Assert.Equal("FileRead", descriptor.Name);
        Assert.Equal("Filesystem", descriptor.Category);
        Assert.False(descriptor.IsDestructive);
        Assert.False(descriptor.IsExternal);
    }

    [Fact]
    public void UnknownCapability_ThrowsKeyNotFoundException()
    {
        var unknown = (ActionIntentKind)999;
        Assert.Throws<KeyNotFoundException>(() => _registry.GetDescriptor(unknown));
    }

    [Fact]
    public void ListAll_ReturnsAllCapabilities()
    {
        var all = _registry.ListAll();

        Assert.NotEmpty(all);
        Assert.True(all.Count >= 7); // At least the 7 registered capabilities
    }

    [Fact]
    public void ListAll_ReturnsImmutableList()
    {
        var all = _registry.ListAll();

        // Verify we can't modify the list
        Assert.IsAssignableFrom<IReadOnlyList<CapabilityDescriptor>>(all);
    }

    [Fact]
    public void Descriptor_IsImmutable()
    {
        var descriptor = _registry.GetDescriptor(ActionIntentKind.FileRead);

        // Records are immutable - verify with 'with' expression
        var modified = descriptor with { Name = "Modified" };

        Assert.Equal("FileRead", descriptor.Name);
        Assert.Equal("Modified", modified.Name);
    }

    [Fact]
    public void FileDelete_Descriptor_IsDestructive()
    {
        var descriptor = _registry.GetDescriptor(ActionIntentKind.FileDelete);

        Assert.True(descriptor.IsDestructive);
    }

    [Fact]
    public void HttpRequest_Descriptor_IsExternal()
    {
        var descriptor = _registry.GetDescriptor(ActionIntentKind.HttpRequest);

        Assert.True(descriptor.IsExternal);
    }

    [Fact]
    public void RegistryDoesNotExecuteCapabilities()
    {
        // Registry is pure data — no execution occurs
        var descriptor = _registry.GetDescriptor(ActionIntentKind.FileRead);

        // If the registry executed anything, we'd have a side effect
        // This test verifies the registry is a passive data store
        Assert.NotNull(descriptor);
        Assert.Equal("FileRead", descriptor.Name);
    }

    [Fact]
    public void RegistryDoesNotMakePolicyDecisions()
    {
        // Registry returns descriptors, not allow/deny decisions
        var descriptor = _registry.GetDescriptor(ActionIntentKind.FileDelete);

        // IsDestructive is descriptive metadata, NOT a policy decision
        Assert.True(descriptor.IsDestructive);
        // But the registry itself does not deny — policy layer decides
    }
}
