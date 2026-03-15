namespace ClientManager.Shared.Models.Entities;

/// <summary>
/// Defines a service that clients can be granted access to.
/// </summary>
public record Service
{
    /// <summary>
    /// Unique identifier for the service.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether this service is currently active. Disabled services reject all requests.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// UTC timestamp when this service was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
