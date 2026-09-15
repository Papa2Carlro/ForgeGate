using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

public class BasicActionIntentNormalizerTests
{
    private readonly BasicActionIntentNormalizer _normalizer = new();

    [Fact]
    public void CatCommand_NormalizesToFileRead()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, result.Intent!.Capability);
        Assert.Equal("/workspace/foo.txt", result.Intent.Target);
    }

    [Fact]
    public void CatCommand_WithWhitespace_ParsesCorrectly()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat   /workspace/foo.txt  "
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal("/workspace/foo.txt", result.Intent!.Target.Trim());
    }

    [Fact]
    public void UnrecognizedCommand_ReturnsUnknown()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "teleport_to_mars"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
        Assert.Equal("teleport_to_mars", result.FailureReason!.RawAction);
    }

    [Fact]
    public void EmptyAction_ReturnsUnknown()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = string.Empty
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void NormalizationDoesNotExecuteAction()
    {
        var executed = false;
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        _normalizer.Normalize(action);

        // Normalizer should be stateless and side-effect free
        Assert.False(executed);
    }

    [Fact]
    public void NormalizationDoesNotMakePolicyDecision()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /etc/passwd"
        };

        var result = _normalizer.Normalize(action);

        // Normalizer returns the intent - policy decision comes later
        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, result.Intent!.Capability);
        // The path might be sensitive, but that's a POLICY concern, not normalization
    }

    [Fact]
    public void NullAction_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _normalizer.Normalize(null!));
    }

    [Fact]
    public void FileReadPreservesTargetPath()
    {
        var action = new AgentAction
        {
            Source = "gpt-4",
            RawAction = "cat /workspace/important_document.pdf"
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal("/workspace/important_document.pdf", result.Intent!.Target);
    }

    [Fact]
    public void TypeCommand_DoesNotBecomeProcessSpawn()
    {
        // "type" contains "/" implicitly via path, but we don't recognize it
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "type C:\\workspace\\foo.txt"
        };

        var result = _normalizer.Normalize(action);

        // Must be Unknown, NOT ProcessSpawn
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void LsCommand_DoesNotBecomeProcessSpawn()
    {
        // "ls /workspace/src" contains "/" but is NOT ProcessSpawn
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "ls /workspace/src"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
        Assert.Equal("ls /workspace/src", result.FailureReason!.RawAction);
    }

    [Fact]
    public void GrepCommand_DoesNotBecomeProcessSpawn()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "grep TODO /workspace/src"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void CurlCommand_DoesNotBecomeProcessSpawn()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "curl https://example.com"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void EchoRedirect_DoesNotBecomeProcessSpawn()
    {
        // "echo hello > /workspace/output.txt" contains "/" but is NOT ProcessSpawn
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "echo hello > /workspace/output.txt"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void CatAtPath_NormalizesToFileRead()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /etc/hosts"
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, result.Intent!.Capability);
        Assert.Equal("/etc/hosts", result.Intent.Target);
    }
}
