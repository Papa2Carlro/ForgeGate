using ForgeGate.Domain.Providers;

namespace ForgeGate.Application.Chat.Routing.Capacity;

/// <summary>
/// Represents ownership of one active execution slot for a route.
/// Disposing releases the slot.
/// </summary>
public sealed class RouteCapacityReservation : IDisposable
{
    private readonly Action _release;
    private bool _disposed;

    internal RouteCapacityReservation(Action release)
    {
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    /// <summary>
    /// Releases the capacity slot. Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _release();
        }
    }
}
