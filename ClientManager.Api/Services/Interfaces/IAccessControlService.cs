using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Interfaces;

/// <summary>
/// Evaluates deny-by-default access policies for clients against services.
/// <para>
/// A request must pass every gate in the access pipeline before it is granted:
/// <list type="number">
///   <item>Client and service must exist and be enabled.</item>
///   <item>The client must have an explicit <c>Services[serviceId]</c> entry with <c>IsAllowed = true</c>.</item>
///   <item>Global aggregate rate limit for the service (if configured) must not be exceeded.</item>
///   <item>Per-client-per-service rate limit (if configured) must not be exceeded.</item>
/// </list>
/// Failure at any stage results in a typed exception that the error-handling middleware
/// maps to the appropriate HTTP status code.
/// </para>
/// <para>
/// This interface also supports a read-only accessibility report that peeks at rate limit
/// state without incrementing counters, useful for dashboard views.
/// </para>
/// </summary>
public interface IAccessControlService
{
    /// <summary>
    /// Checks whether a client is permitted to access a specific service.
    /// Runs the full deny-by-default pipeline: existence checks, enabled flags,
    /// allow-list lookup, global rate limit, and per-client rate limit.
    /// On success, rate limit counters are incremented and usage is recorded.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="serviceId">The unique identifier of the service.</param>
    /// <param name="cancellationToken">Cancels the access check pipeline, including any downstream rate limit evaluation.</param>
    /// <returns>The access check result indicating whether access is granted and any rate limit information.</returns>
    Task<AccessCheckResponse> CheckAccessAsync(string clientId, string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a full accessibility report for a client across all registered services.
    /// Peeks at rate limit state without incrementing counters so the result is safe to
    /// call from dashboards and monitoring without affecting actual usage quotas.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancels the report generation.</param>
    /// <returns>The client accessibility report listing all services and their access status.</returns>
    Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(string clientId, CancellationToken cancellationToken = default);
}
