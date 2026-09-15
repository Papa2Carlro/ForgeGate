using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Streaming;

/// <summary>
/// Buffer that collects streaming tokens before commit boundary.
/// Supports transparent failover in pre-commit phase.
/// </summary>
public sealed class StreamingBuffer : IDisposable
{
    private readonly List<string> _bufferedChunks = new();
    private bool _disposed;

    public bool IsCommitted { get; private set; }

    /// <summary>
    /// Adds a chunk to the buffer. Returns false if already committed (silent splice prevention).
    /// </summary>
    public bool TryAddChunk(string chunk)
    {
        if (_disposed || IsCommitted)
            return false;

        _bufferedChunks.Add(chunk);
        return true;
    }

    /// <summary>
    /// Commits the buffer - transitions to committed state.
    /// After commit, no more chunks can be added (prevents silent splice).
    /// </summary>
    public void Commit()
    {
        IsCommitted = true;
    }

    /// <summary>
    /// Gets all buffered chunks (only accessible before commit).
    /// </summary>
    public IReadOnlyList<string> GetBufferedChunks()
    {
        return _bufferedChunks.AsReadOnly();
    }

    /// <summary>
    /// Clears the buffer (for failover reset).
    /// </summary>
    public void Clear()
    {
        _bufferedChunks.Clear();
        IsCommitted = false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _bufferedChunks.Clear();
            _disposed = true;
        }
    }
}
