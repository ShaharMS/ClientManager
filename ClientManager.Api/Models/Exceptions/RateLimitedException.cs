namespace ClientManager.Api.Models.Exceptions;

public class RateLimitedException : Exception
{
    public int? RetryAfterSeconds { get; }

    public RateLimitedException(string message, int? retryAfterSeconds = null)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
