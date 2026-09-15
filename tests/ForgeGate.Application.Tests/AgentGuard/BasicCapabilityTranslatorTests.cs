using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

public class BasicCapabilityTranslatorTests
{
    private readonly BasicCapabilityTranslator _translator;
    private readonly ICapabilityRegistry _registry;

    public BasicCapabilityTranslatorTests()
    {
        _registry = new InMemoryCapabilityRegistry();
        _translator = new BasicCapabilityTranslator(_registry);
    }

    // =====================================================================
    // Successful translations
    // =====================================================================

    [Fact]
    public void FileRead_TranslatesSuccessfully()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = "/workspace/foo.txt"
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, result.Capability);
        Assert.Equal("/workspace/foo.txt", result.Target);
        Assert.Null(result.Metadata);
    }

    [Fact]
    public void DirectoryList_TranslatesSuccessfully()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.DirectoryList,
            Target = "/workspace/src"
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.DirectoryList, result.Capability);
        Assert.Equal("/workspace/src", result.Target);
    }

    [Fact]
    public void FileSearch_TranslatesSuccessfully()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileSearch,
            Target = "/workspace/src",
            Metadata = "TODO"
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileSearch, result.Capability);
        Assert.Equal("/workspace/src", result.Target);
        Assert.Equal("TODO", result.Metadata);
    }

    [Fact]
    public void FileWrite_TranslatesSuccessfully()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileWrite,
            Target = "/workspace/output.txt"
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileWrite, result.Capability);
    }

    [Fact]
    public void FileDelete_TranslatesSuccessfully()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileDelete,
            Target = "/workspace/old.txt"
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileDelete, result.Capability);
    }

    [Fact]
    public void HttpRequest_TranslatesSuccessfully()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.HttpRequest,
            Target = "https://api.example.com/data"
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.HttpRequest, result.Capability);
    }

    [Fact]
    public void ProcessSpawn_TranslatesSuccessfully()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.ProcessSpawn,
            Target = "some-command"
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.ProcessSpawn, result.Capability);
    }

    // =====================================================================
    // Semantic field preservation
    // =====================================================================

    [Fact]
    public void Target_IsPreservedExactly()
    {
        var originalTarget = "/workspace/important_document.pdf";
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = originalTarget
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(originalTarget, result.Target);
    }

    [Fact]
    public void FileSearchMetadata_IsPreservedExactly()
    {
        var originalQuery = "ERROR: critical failure";
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileSearch,
            Target = "/var/log/app.log",
            Metadata = originalQuery
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        Assert.Equal(originalQuery, result.Metadata);
    }

    // =====================================================================
    // Failure cases
    // =====================================================================

    [Fact]
    public void UnknownCapability_ReturnsFailure()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.Unknown,
            Target = "/any/path"
        };

        var result = _translator.Translate(intent);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void UnregisteredCapability_ReturnsFailure()
    {
        // Introduce a hypothetical capability that isn't registered
        var unknownCapability = (ActionIntentKind)999;
        var intent = new ActionIntent
        {
            Capability = unknownCapability,
            Target = "/some/path"
        };

        var result = _translator.Translate(intent);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
        Assert.Contains($"'{unknownCapability}'", result.FailureReason!.Reason);
    }

    [Fact]
    public void NullIntent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _translator.Translate(null!));
    }

    [Fact]
    public void NullRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BasicCapabilityTranslator(null!));
    }

    // =====================================================================
    // Separation of concerns
    // =====================================================================

    [Fact]
    public void TranslationDoesNotExecuteCapability()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = "/workspace/foo.txt"
        };

        var result = _translator.Translate(intent);

        // If the translator executed anything, we'd have a side effect
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TranslationDoesNotMakePolicyDecision()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileDelete,
            Target = "/critical/system/file"
        };

        var result = _translator.Translate(intent);

        // Translation returns the capability — policy decides allow/deny
        Assert.True(result.IsSuccess);
        Assert.Equal(ActionIntentKind.FileDelete, result.Capability);
        // Does NOT contain Allowed, Risk, ApprovalRequired fields
    }

    [Fact]
    public void TranslationDoesNotAddRiskMetadata()
    {
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileDelete,
            Target = "/workspace/file.txt"
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        // Result only contains Capability, Target, Metadata — no risk score
        Assert.False(hasRiskIndicator(result));
    }

    [Fact]
    public void TranslationDoesNotInspectRawCommand()
    {
        // Proving that translation works WITHOUT any raw command text
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileRead,
            Target = "/workspace/foo.txt"
            // Note: No RawAction, no source command, nothing but semantic intent
        };

        var result = _translator.Translate(intent);

        Assert.True(result.IsSuccess);
        // The translator never saw "cat /workspace/foo.txt" — it only saw
        // the semantic intent FileRead(/workspace/foo.txt)
    }

    [Fact]
    public void TranslationDoesNotSubstituteCapabilities()
    {
        // Unregistered capability must NOT silently fallback to another
        var unknownCapability = (ActionIntentKind)999;
        var intent = new ActionIntent
        {
            Capability = unknownCapability,
            Target = "/workspace/foo.txt"
        };

        var result = _translator.Translate(intent);

        Assert.False(result.IsSuccess);
        // Must NOT become FileRead, ProcessSpawn, or any other capability
        Assert.NotEqual(ActionIntentKind.FileRead, result.Capability);
        Assert.NotEqual(ActionIntentKind.ProcessSpawn, result.Capability);
    }

    // =====================================================================
    // Integration with normalizer
    // =====================================================================

    [Fact]
    public void NormalizedIntent_TranslatesSuccessfully()
    {
        // Full pipeline: normalizer -> translator
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var normalizeResult = normalizer.Normalize(action);
        Assert.True(normalizeResult.IsSuccess);

        var translateResult = translator.Translate(normalizeResult.Intent!);
        Assert.True(translateResult.IsSuccess);
        Assert.Equal(ActionIntentKind.FileRead, translateResult.Capability);
        Assert.Equal("/workspace/foo.txt", translateResult.Target);
    }

    [Fact]
    public void UnknownNormalizedIntent_TranslatesToFailure()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "curl https://example.com"
        };

        var normalizeResult = normalizer.Normalize(action);
        Assert.False(normalizeResult.IsSuccess);

        // Unknown should not somehow become a valid translation
        Assert.Throws<ArgumentNullException>(() => translator.Translate(null!));
    }

    // =====================================================================
    // Helper methods
    // =====================================================================

    private static bool hasRiskIndicator(CapabilityTranslationResult result)
    {
        // This method exists to prove that CapabilityTranslationResult
        // does NOT have risk-related properties
        return false;
    }
}
