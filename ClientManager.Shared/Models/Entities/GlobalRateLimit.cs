namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// A system-wide rate limit shared across all clients for one service.
/// <see cref="Id"/> is the service identifier.
/// </summary>
public record GlobalRateLimit
{
    /// <summary>
    /// Service identifier. Also the document identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Aggregate rate-limit policy applied across all contributing clients.
    /// </summary>
    public RateLimitPolicy Policy { get; init; } = new();

    /// <summary>
    /// UTC timestamp when this global rate limit was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
