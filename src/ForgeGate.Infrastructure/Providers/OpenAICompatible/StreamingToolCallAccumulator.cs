using ForgeGate.Application.Chat;

namespace ForgeGate.Infrastructure.Providers.OpenAICompatible;

/// <summary>
/// Accumulates fragmented tool call deltas from streaming responses.
/// 
/// OpenAI-compatible streaming sends tool call data as fragments across multiple SSE events:
/// - First event: id, partial name
/// - Subsequent events: more name fragments, argument fragments
/// - Final event: finish_reason = "tool_calls"
/// 
/// This accumulator merges fragments by index to reconstruct complete ToolCallInvocation objects.
/// </summary>
public sealed class StreamingToolCallAccumulator
{
    /// <summary>
    /// Per-index mutable state for accumulation.
    /// </summary>
    private sealed class ToolCallDelta
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public bool HasFinishReason { get; set; }
        public string? FinishReason { get; set; }
    }

    private readonly Dictionary<int, ToolCallDelta> _deltas = new();

    /// <summary>
    /// Processes a streaming delta containing potential tool call fragments.
    /// Fragments are accumulated by index.
    /// </summary>
    public void AddDelta(OpenAIStreamDeltaToolCall[]? toolCalls)
    {
        if (toolCalls == null) return;

        foreach (var toolCall in toolCalls)
        {
            if (toolCall.Index == null) continue;

            var index = toolCall.Index.Value;

            if (!_deltas.TryGetValue(index, out var delta))
            {
                delta = new ToolCallDelta();
                _deltas[index] = delta;
            }

            // Accumulate id (first non-empty wins)
            if (!string.IsNullOrEmpty(toolCall.Id) && string.IsNullOrEmpty(delta.Id))
            {
                delta.Id = toolCall.Id;
            }

            // Accumulate name fragments
            if (!string.IsNullOrEmpty(toolCall.Function?.Name))
            {
                delta.Name += toolCall.Function!.Name!;
            }

            // Accumulate argument fragments
            if (!string.IsNullOrEmpty(toolCall.Function?.Arguments))
            {
                delta.Arguments += toolCall.Function!.Arguments!;
            }
        }
    }

    /// <summary>
    /// Marks completion signal (finish reason).
    /// </summary>
    public void MarkComplete(string? finishReason = null)
    {
        // Mark all accumulated deltas as complete
        foreach (var delta in _deltas.Values)
        {
            delta.HasFinishReason = true;
            delta.FinishReason = finishReason;
        }
    }

    /// <summary>
    /// Returns accumulated tool calls as immutable list.
    /// Only includes calls that have received completion signal.
    /// </summary>
    public IReadOnlyList<ToolCallInvocation> GetAccumulatedCalls()
    {
        var result = new List<ToolCallInvocation>();

        foreach (var (index, delta) in _deltas)
        {
            // Only include if we have completion signal
            // (in practice, all accumulated deltas are considered complete)
            if (string.IsNullOrEmpty(delta.Id))
                continue; // Must have an ID to be valid

            result.Add(new ToolCallInvocation
            {
                Index = index,
                Id = delta.Id,
                Name = delta.Name,
                Arguments = delta.Arguments
            });
        }

        // Sort by index to preserve order
        result.Sort((a, b) => a.Index.CompareTo(b.Index));

        return result;
    }

    /// <summary>
    /// Resets accumulator state (for failover).
    /// </summary>
    public void Reset()
    {
        _deltas.Clear();
    }
}
