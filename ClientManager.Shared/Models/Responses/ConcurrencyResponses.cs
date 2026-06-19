namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Result of attempting to reserve allocation counters before creating an allocation.
/// </summary>
public record SlotReservationResult(bool Succeeded, int PoolCount, int ClientCount);

/// <summary>
/// Result of an atomic token-bucket consumption attempt.
/// </summary>
public record TokenBucketConsumeResult(
    bool IsAllowed,
    int RemainingRequests,
    int RetryAfterSeconds);
