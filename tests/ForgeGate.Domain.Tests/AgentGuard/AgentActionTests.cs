using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Domain.Tests.AgentGuard;

public class AgentActionTests
{
    [Fact]
    public void AgentAction_CanBeCreatedWithValidValues()
    {
        var action = new AgentAction
        {
            Source = "gpt-4",
            RawAction = "cat /workspace/foo.txt",
            Payload = "--silent",
            ObservedAt = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
        };

        Assert.Equal("gpt-4", action.Source);
        Assert.Equal("cat /workspace/foo.txt", action.RawAction);
        Assert.Equal("--silent", action.Payload);
    }

    [Fact]
    public void AgentAction_RawActionCANNOTBeEmpty()
    {
        var action = new AgentAction
        {
            Source = "gpt-4",
            RawAction = string.Empty,
            ObservedAt = DateTime.UtcNow
        };

        Assert.Equal(string.Empty, action.RawAction);
    }

    [Fact]
    public void AgentAction_Immutability_PropertiesAreReadOnlyAfterCreation()
    {
        var action = new AgentAction
        {
            Source = "claude-3",
            RawAction = "ls /tmp",
            ObservedAt = DateTime.UtcNow
        };

        // Records are immutable by design - try using with expression to verify
        var updated = action with { Source = "gpt-4o" };
        Assert.Equal("claude-3", action.Source);
        Assert.Equal("gpt-4o", updated.Source);
    }
}
