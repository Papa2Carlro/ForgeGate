using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Domain.Tests.AgentGuard;

public class CapabilityDescriptorTests
{
    [Fact]
    public void FileRead_Descriptor_IsNotDestructive()
    {
        var descriptor = CapabilityDescriptor.FileRead();

        Assert.Equal(ActionIntentKind.FileRead, descriptor.Id);
        Assert.Equal("FileRead", descriptor.Name);
        Assert.Equal("Filesystem", descriptor.Category);
        Assert.False(descriptor.IsDestructive);
        Assert.False(descriptor.IsExternal);
    }

    [Fact]
    public void FileWrite_Descriptor_IsNotDestructive()
    {
        var descriptor = CapabilityDescriptor.FileWrite();

        Assert.Equal(ActionIntentKind.FileWrite, descriptor.Id);
        Assert.Equal("FileWrite", descriptor.Name);
        Assert.Equal("Filesystem", descriptor.Category);
        Assert.False(descriptor.IsDestructive);
    }

    [Fact]
    public void FileDelete_Descriptor_IsDestructive()
    {
        var descriptor = CapabilityDescriptor.FileDelete();

        Assert.Equal(ActionIntentKind.FileDelete, descriptor.Id);
        Assert.Equal("FileDelete", descriptor.Name);
        Assert.True(descriptor.IsDestructive);
        Assert.False(descriptor.IsExternal);
    }

    [Fact]
    public void HttpRequest_Descriptor_IsExternal()
    {
        var descriptor = CapabilityDescriptor.HttpRequest();

        Assert.Equal(ActionIntentKind.HttpRequest, descriptor.Id);
        Assert.Equal("HttpRequest", descriptor.Name);
        Assert.Equal("Network", descriptor.Category);
        Assert.False(descriptor.IsDestructive);
        Assert.True(descriptor.IsExternal);
    }

    [Fact]
    public void ProcessSpawn_Descriptor_IsNotDestructiveNorExternal()
    {
        var descriptor = CapabilityDescriptor.ProcessSpawn();

        Assert.Equal(ActionIntentKind.ProcessSpawn, descriptor.Id);
        Assert.Equal("ProcessSpawn", descriptor.Name);
        Assert.Equal("Process", descriptor.Category);
        Assert.False(descriptor.IsDestructive);
        Assert.False(descriptor.IsExternal);
    }

    [Fact]
    public void Descriptor_CanBeCreatedWithCustomDescription()
    {
        var descriptor = CapabilityDescriptor.FileRead("Read custom description");

        Assert.Equal("Read custom description", descriptor.Description);
    }

    [Fact]
    public void Descriptor_Immutability_Verified()
    {
        var descriptor = CapabilityDescriptor.FileRead();

        // Records are immutable - verify with 'with' expression
        var updated = descriptor with { Name = "Modified" };

        Assert.Equal("FileRead", descriptor.Name);
        Assert.Equal("Modified", updated.Name);
    }

    [Fact]
    public void DirectoryList_Descriptor()
    {
        var descriptor = CapabilityDescriptor.DirectoryList();

        Assert.Equal(ActionIntentKind.DirectoryList, descriptor.Id);
        Assert.Equal("DirectoryList", descriptor.Name);
        Assert.Equal("Directory", descriptor.Category);
        Assert.False(descriptor.IsDestructive);
    }

    [Fact]
    public void FileSearch_Descriptor()
    {
        var descriptor = CapabilityDescriptor.FileSearch();

        Assert.Equal(ActionIntentKind.FileSearch, descriptor.Id);
        Assert.Equal("FileSearch", descriptor.Name);
        Assert.Equal("Search", descriptor.Category);
        Assert.False(descriptor.IsDestructive);
    }
}
