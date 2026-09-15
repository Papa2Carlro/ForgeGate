using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Domain.Tests.AgentGuard;

public class ActionIntentResultTests
{
    [Fact]
    public void Success_CreatesResultWithIntent()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = "/workspace/test.txt"
        };

        var result = ActionIntentResult.Success(intent);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Intent);
        Assert.Equal(ActionIntentKind.FileRead, result.Intent!.Capability);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Unknown_CreatesResultWithFailureReason()
    {
        var rawAction = "run_complex_script_with_unknown_syntax";
        var result = ActionIntentResult.Unknown(rawAction);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
        Assert.Equal(rawAction, result.FailureReason!.RawAction);
        Assert.Null(result.Intent);
    }

    [Fact]
    public void Unknown_DoesNotImpliedAllowOrBlock()
    {
        var result = ActionIntentResult.Unknown("??unrecognized??");

        // Unknown is neither Allow nor Block - it's a distinct state
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }
}
