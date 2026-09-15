using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Domain.Tests.AgentGuard;

public class ActionIntentTests
{
    [Fact]
    public void ActionIntent_FileRead_HasSemanticCapabilityNotCommand()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = "/workspace/foo.txt"
        };

        Assert.Equal(ActionIntentKind.FileRead, intent.Capability);
        Assert.Equal("/workspace/foo.txt", intent.Target);
        // Verify it's NOT command-oriented
        Assert.DoesNotContain("cat", intent.Capability.ToString().ToLower());
    }

    [Fact]
    public void ActionIntent_TargetCanBeAbsolutePath()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileWrite,
            Target = "/home/user/output.log"
        };

        Assert.StartsWith("/", intent.Target);
    }

    [Fact]
    public void ActionIntent_MetadataCanBeNull()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.ProcessSpawn,
            Target = "some_command"
        };

        Assert.Null(intent.Metadata);
    }

    [Fact]
    public void ActionIntent_MetadataCanBeSet()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = "/etc/hosts",
            Metadata = "system_config"
        };

        Assert.Equal("system_config", intent.Metadata);
    }
}
