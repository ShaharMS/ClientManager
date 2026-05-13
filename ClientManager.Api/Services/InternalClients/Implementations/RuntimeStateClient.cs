using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients;
using ClientManager.Api.Services.InternalClients.Interfaces;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.InternalClients.Implementations;

internal sealed class RuntimeStateClient(HttpClient httpClient) : IRuntimeStateClient
{
    public async Task<AccessCheckResponse> CheckAccessAsync(
        CheckAccessRequest request,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            StorageApiRoutes.Runtime.CheckAccess,
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await StorageApiResponseReader.ReadRequiredAsync<AccessCheckResponse>(
                response,
                cancellationToken,
                "The storage API returned an empty access response.");
        }

        throw await MapAccessProblemAsync(response, request, cancellationToken);
    }

    public async Task<ClientAccessibilityResponse> GetAccessibilityAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            StorageApiRoutes.Runtime.GetAccessibility(clientId),
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await StorageApiResponseReader.ReadRequiredAsync<ClientAccessibilityResponse>(
                response,
                cancellationToken,
                "The storage API returned an empty accessibility response.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ClientNotFoundException(clientId);
        }

        throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
    }

    public async Task<ResourceAcquireResponse> AcquireAsync(
        AcquireResourceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            StorageApiRoutes.Runtime.AcquireResource,
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await StorageApiResponseReader.ReadRequiredAsync<ResourceAcquireResponse>(
                response,
                cancellationToken,
                "The storage API returned an empty resource acquire response.");
        }

        throw await MapAcquireProblemAsync(response, request, cancellationToken);
    }

    public async Task<ResourceReleaseResponse> ReleaseAsync(
        ReleaseResourceRequest request,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            StorageApiRoutes.Runtime.ReleaseResource,
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await StorageApiResponseReader.ReadRequiredAsync<ResourceReleaseResponse>(
                response,
                cancellationToken,
                "The storage API returned an empty resource release response.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AllocationNotFoundException(request.AllocationId);
        }

        throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
    }

    private static async Task<Exception> MapAccessProblemAsync(
        HttpResponseMessage response,
        CheckAccessRequest request,
        CancellationToken cancellationToken)
    {
        var problem = await StorageApiResponseReader.ReadProblemAsync(response, cancellationToken);
        var retryAfterSeconds = StorageApiResponseReader.GetRetryAfterSeconds(response);

        return problem.ErrorCode switch
        {
            StorageErrorCodes.ClientNotFound => new ClientNotFoundException(request.ClientId),
            StorageErrorCodes.ServiceNotFound => new ServiceNotFoundException(request.ServiceId),
            StorageErrorCodes.AccessNotConfigured => new AccessNotConfiguredException(request.ClientId, request.ServiceId),
            StorageErrorCodes.AccessDenied => new AccessDeniedException(request.ClientId, request.ServiceId),
            StorageErrorCodes.ClientDisabled => new ClientDisabledException(request.ClientId),
            StorageErrorCodes.ServiceDisabled => new ServiceDisabledException(request.ServiceId),
            StorageErrorCodes.GlobalServiceRateLimitExceeded => new GlobalServiceRateLimitExceededException(retryAfterSeconds),
            StorageErrorCodes.ClientRateLimitExceeded => new ClientRateLimitExceededException(retryAfterSeconds),
            _ => StorageApiResponseReader.CreateUnexpectedException(response, problem)
        };
    }

    private static async Task<Exception> MapAcquireProblemAsync(
        HttpResponseMessage response,
        AcquireResourceRequest request,
        CancellationToken cancellationToken)
    {
        var problem = await StorageApiResponseReader.ReadProblemAsync(response, cancellationToken);
        var retryAfterSeconds = StorageApiResponseReader.GetRetryAfterSeconds(response);

        return problem.ErrorCode switch
        {
            StorageErrorCodes.ClientNotFound => new ClientNotFoundException(request.ClientId),
            StorageErrorCodes.ResourcePoolNotFound => new ResourcePoolNotFoundException(request.ResourcePoolId),
            StorageErrorCodes.ClientDisabled => new ClientDisabledException(request.ClientId),
            StorageErrorCodes.ClientSlotLimitReached => new ClientSlotLimitReachedException(request.ResourcePoolId),
            StorageErrorCodes.GlobalResourcePoolRateLimitExceeded => new GlobalResourcePoolRateLimitExceededException(retryAfterSeconds),
            StorageErrorCodes.NoSlotsAvailable => new NoSlotsAvailableException(request.ResourcePoolId),
            _ => StorageApiResponseReader.CreateUnexpectedException(response, problem)
        };
    }
}