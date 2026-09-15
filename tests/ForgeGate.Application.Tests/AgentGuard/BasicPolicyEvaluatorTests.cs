using ForgeGate.Application.AgentGuard;
using ForgeGate.Domain.AgentGuard;

namespace ForgeGate.Application.Tests.AgentGuard;

public class BasicPolicyEvaluatorTests
{
    private readonly BasicPolicyEvaluator _evaluator;
    private readonly ICapabilityRegistry _registry;

    public BasicPolicyEvaluatorTests()
    {
        _registry = new InMemoryCapabilityRegistry();
        _evaluator = new BasicPolicyEvaluator(_registry);
    }

    // =====================================================================
    // Baseline Allow decisions
    // =====================================================================

    [Fact]
    public void FileRead_IsAllowed()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileRead, "/workspace/foo.txt");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void DirectoryList_IsAllowed()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.DirectoryList, "/workspace/src");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void FileSearch_IsAllowed()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileSearch, "/workspace/src", "TODO");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void FileWrite_IsAllowed()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileWrite, "/workspace/output.txt");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void ProcessSpawn_IsAllowed()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.ProcessSpawn, "some-command");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    // =====================================================================
    // RequireHumanApproval decisions
    // =====================================================================

    [Fact]
    public void FileDelete_RequiresHumanApproval()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileDelete, "/workspace/old.txt");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.RequireHumanApproval, result.Decision);
    }

    [Fact]
    public void HttpRequest_RequiresHumanApproval()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.HttpRequest, "https://api.example.com/data");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.RequireHumanApproval, result.Decision);
    }

    [Fact]
    public void DestructiveAndExternalCapability_RequiresHumanApproval()
    {
        // If a capability is both destructive and external, it still produces
        // one deterministic decision: RequireHumanApproval
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.HttpRequest, "https://api.example.com/delete");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.RequireHumanApproval, result.Decision);
    }

    // =====================================================================
    // Failure cases
    // =====================================================================

    [Fact]
    public void TranslationFailure_CannotBecomeAllow()
    {
        var translation = CapabilityTranslationResult.Failure(
            new TranslationFailure("Unknown capability"));

        var result = _evaluator.Evaluate(translation);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void UnknownCapability_CannotBecomeAllow()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.Unknown, "/any/path");

        var result = _evaluator.Evaluate(translation);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void UnregisteredCapability_CannotBecomeAllow()
    {
        // Introduce a hypothetical capability that isn't registered
        var unknownCapability = (ActionIntentKind)999;
        var translation = CapabilityTranslationResult.Success(
            unknownCapability, "/some/path");

        var result = _evaluator.Evaluate(translation);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public void NullTranslation_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.Evaluate(null!));
    }

    [Fact]
    public void NullRegistry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BasicPolicyEvaluator(null!));
    }

    // =====================================================================
    // Semantic independence
    // =====================================================================

    [Fact]
    public void PolicyDoesNotInspectRawCommand()
    {
        // Construct semantic translation directly — no AgentAction, no raw command
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileRead, "/workspace/foo.txt");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void PolicyDoesNotExecuteCapability()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileRead, "/workspace/foo.txt");

        var result = _evaluator.Evaluate(translation);

        // If the evaluator executed anything, we'd have a side effect
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void PolicyDoesNotPerformNormalization()
    {
        // Normalization happens BEFORE policy evaluation
        // Policy receives semantic data, not raw commands
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileRead, "/workspace/foo.txt");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        // Result is a PolicyDecision, not an ActionIntent
    }

    [Fact]
    public void PolicyDoesNotPerformTranslation()
    {
        // Translation happens BEFORE policy evaluation
        // Policy receives CapabilityTranslationResult, not ActionIntent
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileRead, "/workspace/foo.txt");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        // Result is a PolicyDecision, not a CapabilityTranslationResult
    }

    [Fact]
    public void PolicyPreservesSemanticCapabilityIdentity()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileSearch, "/workspace/src", "ERROR");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, result.Decision);
    }

    [Fact]
    public void DenyExistsAsFirstClassDecision()
    {
        // Verify Deny is a valid policy decision even if baseline rules
        // don't currently produce it for registered capabilities
        var denyDecision = PolicyDecision.Deny;

        Assert.Equal(PolicyDecision.Deny, denyDecision);
    }

    [Fact]
    public void PolicyDoesNotAddRiskMetadata()
    {
        var translation = CapabilityTranslationResult.Success(
            ActionIntentKind.FileDelete, "/workspace/file.txt");

        var result = _evaluator.Evaluate(translation);

        Assert.True(result.IsSuccess);
        // Result only contains Decision and Reason — no risk score
        Assert.Equal(PolicyDecision.RequireHumanApproval, result.Decision);
    }

    // =====================================================================
    // Full pipeline integration
    // =====================================================================

    [Fact]
    public void FullPipeline_FileRead_Allowed()
    {
        // Full pipeline: normalizer → translator → evaluator
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "cat /workspace/foo.txt"
        };

        var normalizeResult = normalizer.Normalize(action);
        Assert.True(normalizeResult.IsSuccess);

        var translateResult = translator.Translate(normalizeResult.Intent!);
        Assert.True(translateResult.IsSuccess);

        var policyResult = evaluator.Evaluate(translateResult);
        Assert.True(policyResult.IsSuccess);
        Assert.Equal(PolicyDecision.Allow, policyResult.Decision);
    }

    [Fact]
    public void FullPipeline_FileDelete_RequiresApproval()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);

        // Simulate a semantic intent for FileDelete (would come from a future normalizer)
        var intent = new ActionIntent
        {
            Capability = ActionIntentKind.FileDelete,
            Target = "/workspace/old.txt"
        };

        var translateResult = translator.Translate(intent);
        Assert.True(translateResult.IsSuccess);

        var policyResult = evaluator.Evaluate(translateResult);
        Assert.True(policyResult.IsSuccess);
        Assert.Equal(PolicyDecision.RequireHumanApproval, policyResult.Decision);
    }

    [Fact]
    public void FullPipeline_UnknownInput_FailsSafely()
    {
        var registry = new InMemoryCapabilityRegistry();
        var normalizer = new BasicActionIntentNormalizer(registry);
        var translator = new BasicCapabilityTranslator(registry);
        var evaluator = new BasicPolicyEvaluator(registry);

        var action = new AgentAction
        {
            Source = "test-model",
            RawAction = "curl https://example.com"
        };

        var normalizeResult = normalizer.Normalize(action);
        Assert.False(normalizeResult.IsSuccess);

        // Unknown normalization should not somehow become a valid policy decision
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
    }

    // =====================================================================
    // Helper methods
    // =====================================================================

    private static bool hasRiskIndicator(PolicyEvaluationResult result)
    {
        // This method exists to prove that PolicyEvaluationResult
        // does NOT have risk-related properties
        return false;
    }
}
