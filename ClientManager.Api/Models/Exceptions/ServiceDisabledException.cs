namespace ClientManager.Api.Models.Exceptions;

public class ServiceDisabledException : Exception
{
    public string ServiceId { get; }

    public ServiceDisabledException(string serviceId)
        : base($"Service '{serviceId}' is disabled")
    {
        ServiceId = serviceId;
    }
}