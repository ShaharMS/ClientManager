namespace ClientManager.Api.Services.Storage.RateLimiting;

/// <summary>
/// Computes aligned window boundaries for fixed-window and token-bucket strategies.
/// </summary>
internal static class RateLimitWindowAlignment
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Returns the Unix timestamp of the window start containing <paramref name="unixSeconds"/>.
    /// When <paramref name="anchor"/> is <see langword="null"/>, windows align to the Unix epoch.
    /// </summary>
    public static long GetWindowStart(long unixSeconds, long windowSeconds, TimeSpan? anchor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);

        if (anchor is null)
        {
            return FloorDiv(unixSeconds, windowSeconds) * windowSeconds;
        }

        var origin = Epoch.Add(anchor.Value).ToUnixTimeSeconds();
        var windowIndex = FloorDiv(unixSeconds - origin, windowSeconds);
        return origin + windowIndex * windowSeconds;
    }

    /// <summary>
    /// Returns seconds until the next window boundary after <paramref name="unixSeconds"/>.
    /// </summary>
    public static int GetRetryAfterSeconds(long unixSeconds, long windowSeconds, TimeSpan? anchor)
    {
        var windowStart = GetWindowStart(unixSeconds, windowSeconds, anchor);
        return Math.Max(1, (int)(windowStart + windowSeconds - unixSeconds));
    }

    private static long FloorDiv(long dividend, long divisor)
    {
        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor));
        }

        if (dividend >= 0)
        {
            return dividend / divisor;
        }

        return (dividend - divisor + 1) / divisor;
    }
}
