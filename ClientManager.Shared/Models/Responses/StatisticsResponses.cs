namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// High-level system overview statistics for the dashboard.
/// </summary>
/// <param name="TotalClients">Total number of client configurations.</param>
/// <param name="TotalServices">Total number of service definitions.</param>
/// <param name="RequestsPerMinute">Estimated requests per minute across all services.</param>
public record SystemOverviewResponse(
    int TotalClients,
    int TotalServices,
    double RequestsPerMinute);
