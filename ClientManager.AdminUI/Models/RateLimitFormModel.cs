using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Models;

/// <summary>
/// Form model for creating or editing a global per-service rate limit.
/// </summary>
public class RateLimitFormModel
{
    /// <summary>
    /// Document identifier. For new limits this equals <see cref="ServiceId"/>.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Service this global limit applies to.
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    public RateLimitStrategy Strategy { get; set; }
    public int MaxRequests { get; set; } = 100;
    public double WindowSeconds { get; set; } = 60;
    public int? TokensPerRefill { get; set; }
}
