using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.InternalClients.Interfaces;
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
            return await ReadRequiredAsync<AccessCheckResponse>(response, cancellationToken)
                ?? throw new InvalidOperationException("The storage API returned an empty access response.");
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
            return await ReadRequiredAsync<ClientAccessibilityResponse>(response, cancellationToken)
                ?? throw new InvalidOperationException("The storage API returned an empty accessibility response.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ClientNotFoundException(clientId);
        }

        throw await CreateUnexpectedExceptionAsync(response, cancellationToken);
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
            return await ReadRequiredAsync<ResourceAcquireResponse>(response, cancellationToken)
                ?? throw new InvalidOperationException("The storage API returned an empty resource acquire response.");
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
            return await ReadRequiredAsync<ResourceReleaseResponse>(response, cancellationToken)
                ?? throw new InvalidOperationException("The storage API returned an empty resource release response.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AllocationNotFoundException(request.AllocationId);
        }

        throw await CreateUnexpectedExceptionAsync(response, cancellationToken);
    }

    private static async Task<T?> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private static async Task<Exception> MapAccessProblemAsync(
        HttpResponseMessage response,
        CheckAccessRequest request,
        CancellationToken cancellationToken)
    {
        var problem = await ReadProblemAsync(response, cancellationToken);
        var retryAfterSeconds = GetRetryAfterSeconds(response);

        return problem.ErrorCode switch
        {
            "client_not_found" => new ClientNotFoundException(request.ClientId),
            "service_not_found" => new ServiceNotFoundException(request.ServiceId),
            "access_not_configured" => new AccessNotConfiguredException(request.ClientId, request.ServiceId),
            "access_denied" => new AccessDeniedException(request.ClientId, request.ServiceId),
            "client_disabled" => new ClientDisabledException(request.ClientId),
            "service_disabled" => new ServiceDisabledException(request.ServiceId),
            "global_service_rate_limit_exceeded" => new GlobalServiceRateLimitExceededException(retryAfterSeconds),
            "client_rate_limit_exceeded" => new ClientRateLimitExceededException(retryAfterSeconds),
            _ => await CreateUnexpectedExceptionAsync(response, cancellationToken)
        };
    }

    private static async Task<Exception> MapAcquireProblemAsync(
        HttpResponseMessage response,
        AcquireResourceRequest request,
        CancellationToken cancellationToken)
    {
        var problem = await ReadProblemAsync(response, cancellationToken);
        var retryAfterSeconds = GetRetryAfterSeconds(response);

        return problem.ErrorCode switch
        {
            "client_not_found" => new ClientNotFoundException(request.ClientId),
            "resource_pool_not_found" => new ResourcePoolNotFoundException(request.ResourcePoolId),
            "client_disabled" => new ClientDisabledException(request.ClientId),
            "client_slot_limit_reached" => new ClientSlotLimitReachedException(request.ResourcePoolId),
            "global_resource_pool_rate_limit_exceeded" => new GlobalResourcePoolRateLimitExceededException(retryAfterSeconds),
            "no_slots_available" => new NoSlotsAvailableException(request.ResourcePoolId),
            _ => await CreateUnexpectedExceptionAsync(response, cancellationToken)
        };
    }

    private static async Task<StorageApiProblemResponse> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<StorageApiProblemResponse>(cancellationToken)
            ?? new StorageApiProblemResponse
            {
                Status = (int)response.StatusCode,
                Detail = $"The storage API returned status {(int)response.StatusCode}."
            };
    }

    private static int? GetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
        {
            return Math.Max(1, (int)Math.Ceiling(delta.TotalSeconds));
        }

        if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
        {
            var seconds = (int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds);
            return Math.Max(1, seconds);
        }

        if (response.Headers.TryGetValues("Retry-After", out var values)
            && int.TryParse(values.FirstOrDefault(), out var retryAfterSeconds))
        {
            return retryAfterSeconds;
        }

        return null;
    }

    private static async Task<Exception> CreateUnexpectedExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await ReadProblemAsync(response, cancellationToken);
        var detail = problem.Detail ?? $"The storage API returned status {(int)response.StatusCode}.";
        return new HttpRequestException(detail, null, response.StatusCode);
    }

    private sealed record StorageApiProblemResponse
    {
        public string? Title { get; init; }

        public int? Status { get; init; }

        public string? Detail { get; init; }

        public string? ErrorCode { get; init; }

        public string? TraceId { get; init; }
    }
}