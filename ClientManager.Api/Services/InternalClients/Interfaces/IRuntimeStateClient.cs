using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.InternalClients.Interfaces;

// CR: Interface needs documentation. Class should have doc explaining purpose and why it exists somewhat briefly. each method should also have the same documentation - what it does, why it exists, and any important details about behavior/context, with explanitory parameter descriptions.
public interface IRuntimeStateClient
{
    Task<AccessCheckResponse> CheckAccessAsync(CheckAccessRequest request, CancellationToken cancellationToken);

    Task<ClientAccessibilityResponse> GetAccessibilityAsync(string clientId, CancellationToken cancellationToken);

    Task<ResourceAcquireResponse> AcquireAsync(AcquireResourceRequest request, CancellationToken cancellationToken);

    Task<ResourceReleaseResponse> ReleaseAsync(ReleaseResourceRequest request, CancellationToken cancellationToken);
}