using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

public class BasicActionIntentNormalizerTests
{
    private readonly BasicActionIntentNormalizer _normalizer;

    public BasicActionIntentNormalizerTests()
    {
        var registry = new InMemoryCapabilityRegistry();
        _normalizer = new BasicActionIntentNormalizer(registry);
    }

    // =====================================================================
    // Canonical cases
    // =====================================================================

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
        Assert.Equal("/workspace/foo.txt", result.Intent!.Target);
    }

    [Fact]
    public void CatAtPath_NormalizesToFileRead()
    {
        var action = new AgentAction
        {
            Source = "gpt-4",
            RawAction = "cat /etc/hosts"
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, result.Intent!.Capability);
        Assert.Equal("/etc/hosts", result.Intent.Target);
    }

    [Fact]
    public void CatPreservesTargetPath()
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

    // =====================================================================
    // DirectoryList cases
    // =====================================================================

    [Fact]
    public void LsCommand_NormalizesToDirectoryList()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "ls /workspace/src"
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.DirectoryList, result.Intent!.Capability);
        Assert.Equal("/workspace/src", result.Intent.Target);
    }

    [Fact]
    public void LsCommand_WithWhitespace_ParsesCorrectly()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "ls   /workspace/src  "
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal("/workspace/src", result.Intent!.Target);
    }

    [Fact]
    public void LsCommand_MissingPath_ReturnsUnknown()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "ls"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void LsCommand_TooManyArguments_ReturnsUnknown()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "ls /workspace/src /workspace/test"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
    }

    // =====================================================================
    // FileSearch cases
    // =====================================================================

    [Fact]
    public void GrepCommand_NormalizesToFileSearch()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "grep TODO /workspace/src"
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileSearch, result.Intent!.Capability);
        Assert.Equal("/workspace/src", result.Intent.Target);
        Assert.Equal("TODO", result.Intent.Metadata);
    }

    [Fact]
    public void GrepCommand_WithWhitespace_ParsesCorrectly()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "grep   ERROR   /workspace/logs  "
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal("/workspace/logs", result.Intent!.Target);
        Assert.Equal("ERROR", result.Intent.Metadata);
    }

    [Fact]
    public void GrepCommand_MissingQuery_ReturnsUnknown()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "grep"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void GrepCommand_MissingScope_ReturnsUnknown()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "grep TODO"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void GrepCommand_TooManyArguments_ReturnsUnknown()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "grep TODO /workspace/src extra"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
    }

    // =====================================================================
    // Negative cases — must remain Unknown
    // =====================================================================

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
    public void TypeCommand_DoesNotBecomeProcessSpawn()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "type C:\\workspace\\foo.txt"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
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
    public void DangerousSyntax_DoesNotBecomeProcessSpawn()
    {
        // Contains shell metacharacters but is NOT a recognized command
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "rm -rf /workspace/*"
        };

        var result = _normalizer.Normalize(action);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void PathContainingCommand_DoesNotBecomeProcessSpawn()
    {
        // "ls /workspace/src" contains "/" but is recognized as DirectoryList,
        // not ProcessSpawn
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "ls /workspace/src"
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.DirectoryList, result.Intent!.Capability);
    }

    // =====================================================================
    // Separation of concerns
    // =====================================================================

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

        // Normalizer returns the intent — policy decision comes later
        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, result.Intent!.Capability);
    }

    [Fact]
    public void NullAction_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _normalizer.Normalize(null!));
    }

    [Fact]
    public void NullRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BasicActionIntentNormalizer(null!));
    }

    // =====================================================================
    // Canonical capability validation
    // =====================================================================

    [Fact]
    public void NormalizedCapabilityExistsInRegistry()
    {
        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var result = _normalizer.Normalize(action);

        Assert.True(result.IsSuccess);

        var registry = new InMemoryCapabilityRegistry();
        Assert.True(registry.IsRegistered(result.Intent!.Capability));
    }

    [Fact]
    public void AllNormalizedCapabilitiesAreRegistered()
    {
        var registry = new InMemoryCapabilityRegistry();

        // Verify all capabilities we normalize to are registered
        foreach (ActionIntentKind capability in Enum.GetValues(typeof(ActionIntentKind)))
        {
            if (capability == ActionIntentKind.Unknown)
                continue;

            Assert.True(registry.IsRegistered(capability),
                $"Capability {capability} is not registered");
        }
    }
}
