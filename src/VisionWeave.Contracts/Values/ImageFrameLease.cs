namespace VisionWeave.Contracts.Values;

/// <summary>
/// A read-only claim on an image buffer. The producer owns the lease; each
/// scheduled consumer takes a reservation and never disposes the lease itself.
/// </summary>
public abstract class ImageFrameLease : IDisposable
{
    private int _disposed;

    /// <summary>
    /// Initializes a lease with its frame metadata.
    /// </summary>
    /// <param name="width">The frame width in pixels.</param>
    /// <param name="height">The frame height in pixels.</param>
    /// <param name="pixelFormat">The pixel layout of the frame.</param>
    protected ImageFrameLease(int width, int height, FramePixelFormat pixelFormat)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
    }

    /// <summary>
    /// Gets the frame width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the frame height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the pixel layout of the frame.
    /// </summary>
    public FramePixelFormat PixelFormat { get; }

    /// <summary>
    /// Gets a value indicating whether the lease has been released.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Takes a consumer reservation that keeps the lease alive until the
    /// reservation is released.
    /// </summary>
    /// <returns>The reservation to release when the consumer is finished.</returns>
    public ILeaseReservation AddConsumerReservation() => CreateReservation();

    /// <summary>
    /// Releases the producer's ownership of the lease.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            DisposeCore();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates the reservation for the concrete lease implementation.
    /// </summary>
    /// <returns>The reservation to release when the consumer is finished.</returns>
    protected abstract ILeaseReservation CreateReservation();

    /// <summary>
    /// Releases the concrete lease once producer ownership is dropped.
    /// </summary>
    protected abstract void DisposeCore();
}
