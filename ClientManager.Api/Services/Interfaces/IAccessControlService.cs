using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Evaluates deny-by-default access policies for clients against services.
/// </summary>
/// <remarks>
/// <para>
/// A request must pass every gate in the access pipeline before it is granted:
/// </para>
/// <list type="number">
///   <item><description>Client and service must exist and be enabled.</description></item>
///   <item><description>The client must have an explicit <c>Services[serviceId]</c> entry with <c>IsAllowed = true</c>.</description></item>
///   <item><description>Global rate limit for the service (if configured) must not be exceeded.</description></item>
///   <item><description>Per-client-per-service rate limit (if configured) must not be exceeded.</description></item>
/// </list>
/// <para>
/// Failure at any stage results in a typed exception that the error-handling middleware maps to
/// the appropriate HTTP status code and RFC 7807 problem body for nginx and other gateways.
/// </para>
/// <para>
/// Successful checks increment rate-limit counters and record RPM accounting so dashboard
/// statistics stay aligned with the hot path.
/// </para>
/// </remarks>
public interface IAccessControlService
{
    /// <summary>
    /// Checks whether a client is permitted to access a specific service.
    /// </summary>
    /// <remarks>
    /// Runs the full deny-by-default pipeline: existence checks, enabled flags, allow-list lookup,
    /// global rate limit, and per-client rate limit. On success, rate limit counters are incremented
    /// and the request is counted toward RPM.
    /// </remarks>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the access check pipeline, including any downstream rate limit evaluation.</param>
    /// <returns>The access check result indicating whether access is granted and any rate limit information.</returns>
    Task<AccessCheckResponse> CheckAccessAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);
}
