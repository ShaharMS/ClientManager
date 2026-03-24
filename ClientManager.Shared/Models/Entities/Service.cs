namespace ClientManager.Shared.Models.Entities;

using System.Net;

/// <summary>
/// Defines a stateless, request/response target that clients can be granted access to
/// (an API, microservice, or web endpoint).
///
/// <para>
///     Access to a service is controlled per-client through
///     <see cref="ClientConfiguration.Services"/>. Rate limits can be applied per-client
///     (<see cref="ClientRateLimit"/>), per-client-per-service
///     (<see cref="ServiceAccessSettings.RateLimit"/>), and globally
///     (<see cref="GlobalRateLimit"/> with <see cref="Enums.TargetType.Service"/>).
/// </para>
/// </summary>
public record Service
{
    /// <summary>
    /// Unique identifier for the service.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name, used for error messages and metrics tags.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether this service is currently active. Disabled services reject all requests with a <see cref="HttpStatusCode.Forbidden"/> response.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// UTC timestamp when this service was created (or at least, when it was registered in the system).
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
