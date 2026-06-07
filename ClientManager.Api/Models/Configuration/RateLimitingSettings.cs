namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Startup settings that control how fixed-window and token-bucket rate limits align their
/// reset boundaries.
/// </summary>
public sealed class RateLimitingSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// UTC offset (up to 24 hours) of the first window boundary in the global alignment grid.
    /// Boundaries repeat every configured window at <c>anchor + n × window</c>, continuing
    /// across day boundaries without resetting at midnight.
    /// <para>
    /// With a 30-minute window, <c>00:00:00</c> resets on the hour and half-hour while
    /// <c>00:30:00</c> shifts those boundaries by 30 minutes. With a 6-hour window, the
    /// anchor chooses whether limits reset at 00:00/06:00/12:00/18:00, 06:00/12:00/18:00/00:00,
    /// or another phase — the first full interval after midnight therefore starts at the anchor,
    /// not at 00:00.
    /// </para>
    /// <para>
    /// When <see langword="null"/>, windows align to the Unix epoch (legacy behavior).
    /// </para>
    /// </summary>
    public TimeSpan? WindowAlignmentAnchor { get; init; }
}
