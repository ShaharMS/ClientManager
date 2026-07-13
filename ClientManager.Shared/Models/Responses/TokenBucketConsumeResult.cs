namespace ClientManager.Shared.Models.Responses;

/// <summary>
/// Result of an atomic token-bucket consumption attempt in storage.
/// </summary>
/// <param name="IsAllowed">Whether a token was available and consumed.</param>
/// <param name="RemainingRequests">Tokens remaining in the bucket after consumption.</param>
/// <param name="RetryAfterSeconds">Seconds until the next refill when denied.</param>
public record TokenBucketConsumeResult(
    bool IsAllowed,
    int RemainingRequests,
    int RetryAfterSeconds);
