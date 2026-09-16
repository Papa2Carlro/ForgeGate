using ForgeGate.Infrastructure.Providers.OpenAICompatible;

namespace ForgeGate.Application.Tests.Chat.Streaming;

/// <summary>
/// Tests for StreamingToolCallAccumulator.
/// </summary>
public class StreamingToolCallAccumulatorTests
{
    [Fact]
    public void AddDelta_SingleCallWithSingleFragment_AccumulatesCorrectly()
    {
        // Given
        var accumulator = new StreamingToolCallAccumulator();
        var delta = new OpenAIStreamDeltaToolCall[]
        {
            new()
            {
                Index = 0,
                Id = "call_abc",
                Function = new OpenAIStreamDeltaToolCallFunction
                {
                    Name = "file_read",
                    Arguments = "{\"path\": \"/workspace/foo.txt\"}"
                }
            }
        };

        // When
        accumulator.AddDelta(delta);
        accumulator.MarkComplete("tool_calls");
        var calls = accumulator.GetAccumulatedCalls();

        // Then
        Assert.Single(calls);
        Assert.Equal("call_abc", calls[0].Id);
        Assert.Equal("file_read", calls[0].Name);
        Assert.Equal("{\"path\": \"/workspace/foo.txt\"}", calls[0].Arguments);
    }

    [Fact]
    public void AddDelta_MultipleFragmentsForSameCall_ConcatenatesCorrectly()
    {
        // Given
        var accumulator = new StreamingToolCallAccumulator();
        
        // First fragment: id + name
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 0,
                Id = "call_xyz",
                Function = new OpenAIStreamDeltaToolCallFunction { Name = "file_", Arguments = null }
            }
        });
        
        // Second fragment: more arguments
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 0,
                Function = new OpenAIStreamDeltaToolCallFunction { Name = null, Arguments = "{\"path"}
            }
        });
        
        // Third fragment: final arguments
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 0,
                Function = new OpenAIStreamDeltaToolCallFunction { Name = null, Arguments = "\": \"/a.txt\"}" }
            }
        });
        accumulator.MarkComplete("tool_calls");

        // When
        var calls = accumulator.GetAccumulatedCalls();

        // Then
        Assert.Single(calls);
        Assert.Equal("call_xyz", calls[0].Id);
        Assert.Equal("file_", calls[0].Name);
        // Concatenation: "{\"path\"" + "\": \"/a.txt\"}" = "{\"path\": \"/a.txt\"}"
        Assert.Equal("{\"path\": \"/a.txt\"}", calls[0].Arguments);
    }

    [Fact]
    public void AddDelta_MultipleCallsWithDifferentIndexes_PreservesOrder()
    {
        // Given
        var accumulator = new StreamingToolCallAccumulator();
        
        // Call 0
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 0,
                Id = "call_0",
                Function = new OpenAIStreamDeltaToolCallFunction
                {
                    Name = "file_read",
                    Arguments = "{}"
                }
            }
        });
        
        // Call 1
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 1,
                Id = "call_1",
                Function = new OpenAIStreamDeltaToolCallFunction
                {
                    Name = "file_write",
                    Arguments = "{}"
                }
            }
        });
        
        accumulator.MarkComplete("tool_calls");

        // When
        var calls = accumulator.GetAccumulatedCalls();

        // Then
        Assert.Equal(2, calls.Count);
        Assert.Equal("call_0", calls[0].Id);
        Assert.Equal(0, calls[0].Index);
        Assert.Equal("call_1", calls[1].Id);
        Assert.Equal(1, calls[1].Index);
    }

    [Fact]
    public void AddDelta_InterleavedFragmentsForMultipleCalls_CorrectlyIsolates()
    {
        // Given
        var accumulator = new StreamingToolCallAccumulator();
        
        // Start call 0
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 0,
                Id = "call_a",
                Function = new OpenAIStreamDeltaToolCallFunction { Name = "func_a", Arguments = "arg1" }
            }
        });
        
        // Start call 1 (call 0 still pending)
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 1,
                Id = "call_b",
                Function = new OpenAIStreamDeltaToolCallFunction { Name = "func_b", Arguments = "arg2" }
            }
        });
        
        accumulator.MarkComplete("tool_calls");

        // When
        var calls = accumulator.GetAccumulatedCalls();

        // Then
        Assert.Equal(2, calls.Count);
        Assert.Equal("call_a", calls[0].Id);
        Assert.Equal("func_a", calls[0].Name);
        Assert.Equal("call_b", calls[1].Id);
        Assert.Equal("func_b", calls[1].Name);
    }

    [Fact]
    public void AddDelta_EmptyArguments_ProducesEmptyString()
    {
        // Given
        var accumulator = new StreamingToolCallAccumulator();
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 0,
                Id = "call_empty",
                Function = new OpenAIStreamDeltaToolCallFunction
                {
                    Name = "empty_func",
                    Arguments = ""
                }
            }
        });
        accumulator.MarkComplete("tool_calls");

        // When
        var calls = accumulator.GetAccumulatedCalls();

        // Then
        Assert.Single(calls);
        Assert.Equal("", calls[0].Arguments);
    }

    [Fact]
    public void AddDelta_NoId_SkipsCall()
    {
        // Given
        var accumulator = new StreamingToolCallAccumulator();
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 0,
                // No Id set
                Function = new OpenAIStreamDeltaToolCallFunction
                {
                    Name = "test_func",
                    Arguments = "{}"
                }
            }
        });
        accumulator.MarkComplete("tool_calls");

        // When
        var calls = accumulator.GetAccumulatedCalls();

        // Then - calls without id are skipped
        Assert.Empty(calls);
    }

    [Fact]
    public void AddDelta_NullOrEmptyInput_NoException()
    {
        // Given
        var accumulator = new StreamingToolCallAccumulator();

        // When/Then - should not throw
        accumulator.AddDelta(null!);
        accumulator.AddDelta(Array.Empty<OpenAIStreamDeltaToolCall>());
        accumulator.MarkComplete();
        var calls = accumulator.GetAccumulatedCalls();
        Assert.Empty(calls);
    }

    [Fact]
    public void Reset_ClearsAccumulatedState()
    {
        // Given
        var accumulator = new StreamingToolCallAccumulator();
        accumulator.AddDelta(new[]
        {
            new OpenAIStreamDeltaToolCall
            {
                Index = 0,
                Id = "call_reset",
                Function = new OpenAIStreamDeltaToolCallFunction { Name = "test", Arguments = "{}" }
            }
        });
        accumulator.MarkComplete();

        // When
        accumulator.Reset();
        var calls = accumulator.GetAccumulatedCalls();

        // Then
        Assert.Empty(calls);
    }
}
