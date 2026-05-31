using Microsoft.AspNetCore.Http;

namespace ClientManager.Api.Models.Exceptions;

/// <summary>
/// Thrown when a request targets a service whose <c>IsEnabled</c> flag is <c>false</c>.
/// Mapped to HTTP 403 by the error-handling middleware.
/// </summary>
public class ServiceDisabledException : HttpProblemException
{
    public string ServiceId { get; }

    public ServiceDisabledException(string serviceId)
        : base(StatusCodes.Status403Forbidden, "Forbidden", $"Service '{serviceId}' is disabled")
    {
        ServiceId = serviceId;
    }
}