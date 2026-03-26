namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a global rate limit definition cannot be found by its identifier.
/// </summary>
public class GlobalRateLimitNotFoundException(string globalRateLimitId) : NotFoundException($"Global rate limit '{globalRateLimitId}' not found")
{
    public string GlobalRateLimitId { get; } = globalRateLimitId;
}
