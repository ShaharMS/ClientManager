namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a client does not have a global rate limit configured.
/// </summary>
public class ClientGlobalRateLimitNotFoundException(string clientId) : NotFoundException($"No global rate limit configured for client '{clientId}'")
{
    public string ClientId { get; } = clientId;
}
