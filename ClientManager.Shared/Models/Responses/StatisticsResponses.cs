namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// High-level system overview statistics for the Admin UI dashboard.
/// </summary>
/// <remarks>
/// <para>
/// Provides aggregate catalog counts and a live requests-per-minute gauge sourced from the
/// shared in-storage RPM ring (five-minute average).
/// </para>
/// </remarks>
/// <param name="TotalClients">Total number of client configurations.</param>
/// <param name="TotalServices">Total number of service definitions.</param>
/// <param name="RequestsPerMinute">Estimated requests per minute across all services.</param>
public record SystemOverviewResponse(
    int TotalClients,
    int TotalServices,
    double RequestsPerMinute);
