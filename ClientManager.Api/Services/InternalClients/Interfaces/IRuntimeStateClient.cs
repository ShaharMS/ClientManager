using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.InternalClients.Interfaces;

public interface IRuntimeStateClient
{
    Task<AccessCheckResponse> CheckAccessAsync(CheckAccessRequest request, CancellationToken cancellationToken);

    Task<ClientAccessibilityResponse> GetAccessibilityAsync(string clientId, CancellationToken cancellationToken);

    Task<ResourceAcquireResponse> AcquireAsync(AcquireResourceRequest request, CancellationToken cancellationToken);

    Task<ResourceReleaseResponse> ReleaseAsync(ReleaseResourceRequest request, CancellationToken cancellationToken);
}