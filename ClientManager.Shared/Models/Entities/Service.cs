namespace ClientManager.Shared.Models.Entities;

using System.Net;
using ClientManager.Shared.Models.Enums;

/// <summary>
/// Defines a stateless, request/response target that clients can be granted access to
/// (an API, microservice, or web endpoint).
///
/// <para>
///     Access to a service is controlled per-client through
///     <see cref="ClientConfiguration.Services"/>. Rate limits can be applied per-client
///     (<see cref="ClientConfiguration.GlobalRateLimit"/>), per-client-per-service
///     (<see cref="ServiceAccessSettings.RateLimit"/>), and globally
///     (<see cref="GlobalRateLimit"/>).
/// </para>
/// </summary>
public record Service
{
    /// <summary>
    /// Unique identifier for the service.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable display name, used for error messages and metrics tags.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this service is currently active. Disabled services reject all requests with a <see cref="HttpStatusCode.Forbidden"/> response.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// UTC timestamp when this service was created (or at least, when it was registered in the system).
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
