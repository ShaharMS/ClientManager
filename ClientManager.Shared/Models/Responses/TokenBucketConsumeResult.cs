namespace ClientManager.Shared.Models.Responses;

/// <summary>Result of an atomic token-bucket consumption attempt.</summary>
public record TokenBucketConsumeResult(
    bool IsAllowed,
    int RemainingRequests,
    int RetryAfterSeconds);
