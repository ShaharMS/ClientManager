using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a request targets a client whose <c>IsEnabled</c> flag is <c>false</c>.
/// Mapped to HTTP 403 by the error-handling middleware.
/// </summary>
public class ClientDisabledException : HttpProblemException
{
    public string ClientId { get; }

    public ClientDisabledException(string clientId)
        : base(StatusCodes.Status403Forbidden, "Forbidden", $"Client '{clientId}' is disabled")
    {
        ClientId = clientId;
    }
}
