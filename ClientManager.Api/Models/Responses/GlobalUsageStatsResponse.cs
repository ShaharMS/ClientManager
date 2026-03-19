namespace ClientManager.Api.Models.Responses;

/// <summary>
/// Global usage statistics including request rate and pool acquisition.
/// </summary>
/// <param name="RequestsPerMinute">Estimated requests per minute across the system.</param>
/// <param name="TotalPoolSlots">Total number of pool slots across all resource pools.</param>
/// <param name="AcquiredPoolSlots">Number of pool slots currently acquired.</param>
/// <param name="AcquisitionPercentage">Percentage of pool slots currently acquired.</param>
public record GlobalUsageStatsResponse(
    double RequestsPerMinute,
    int TotalPoolSlots,
    int AcquiredPoolSlots,
    double AcquisitionPercentage
);
