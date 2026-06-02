using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Internal.Interfaces;
using ClientManager.Api.Utils.Instrumentation;
using ClientManager.Api.Utils.StorageApi;
using ClientManager.Shared.Contracts.Storage;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Problems;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Internal.Implementations;

/// <summary>
/// Typed HTTP client over the storage API's runtime routes for access checks and resource allocation.
/// Owns the client-side instrumentation (tracing, metrics, structured logging) for these hot-path calls
/// and maps storage problem responses onto domain exceptions for the public runtime controllers.
/// </summary>
internal sealed class RuntimeStateClient : IRuntimeStateClient
{
    private const double SlowStorageCallThresholdMs = 250;

    private static readonly StorageClientOperation AccessCheckOperation = new(
        "access_check",
        HttpMethod.Post.Method,
        "/api/v1/access/check",
        StorageApiRoutes.Runtime.CheckAccess);

    private static readonly StorageClientOperation AcquireOperation = new(
        "resource_acquire",
        HttpMethod.Post.Method,
        "/api/v1/resources/acquire",
        StorageApiRoutes.Runtime.AcquireResource);

    private static readonly StorageClientOperation ReleaseOperation = new(
        "resource_release",
        HttpMethod.Post.Method,
        "/api/v1/resources/release",
        StorageApiRoutes.Runtime.ReleaseResource);

    private readonly HttpClient _httpClient;
    private readonly ClientManagerMetrics _metrics;
    private readonly IAppLogger<RuntimeStateClient> _logger;

    public RuntimeStateClient(
        HttpClient httpClient,
        ClientManagerMetrics metrics,
        IAppLogger<RuntimeStateClient> logger)
    {
        _httpClient = httpClient;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AccessCheckResponse> CheckAccessAsync(
        CheckAccessRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = StartStorageCall(AccessCheckOperation);
        activity?.SetTag("client.id", request.ClientId);
        activity?.SetTag("service.id", request.ServiceId);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        Exception? exception = null;
        var result = "unknown";

        try
        {
            response = await _httpClient.PostAsJsonAsync(
                AccessCheckOperation.StorageRoute,
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                result = GetFailureResult(response.StatusCode);
                throw await MapAccessProblemAsync(response, request, cancellationToken);
            }

            var accessResponse = await StorageApiResponseReader.ReadRequiredAsync<AccessCheckResponse>(
                response,
                cancellationToken,
                "The storage API returned an empty access response.");

            result = "granted";
            return accessResponse;
        }
        catch (OperationCanceledException caught) when (cancellationToken.IsCancellationRequested)
        {
            result = "canceled";
            exception = caught;
            throw;
        }
        catch (Exception caught)
        {
            exception = caught;
            throw;
        }
        finally
        {
            CompleteStorageCall(
                AccessCheckOperation,
                stopwatch,
                response,
                result,
                exception,
                request.ClientId,
                serviceId: request.ServiceId);
        }
    }

    /// <inheritdoc />
    public async Task<ClientAccessibilityResponse> GetAccessibilityAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
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

    /// <inheritdoc />
    public async Task<ResourceAcquireResponse> AcquireAsync(
        AcquireResourceRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = StartStorageCall(AcquireOperation);
        activity?.SetTag("client.id", request.ClientId);
        activity?.SetTag("resource_pool.id", request.ResourcePoolId);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        Exception? exception = null;
        var result = "unknown";

        try
        {
            response = await _httpClient.PostAsJsonAsync(
                AcquireOperation.StorageRoute,
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                result = GetFailureResult(response.StatusCode);
                throw await MapAcquireProblemAsync(response, request, cancellationToken);
            }

            var acquireResponse = await StorageApiResponseReader.ReadRequiredAsync<ResourceAcquireResponse>(
                response,
                cancellationToken,
                "The storage API returned an empty resource acquire response.");

            result = "acquired";
            activity?.SetTag("allocation.id", acquireResponse.AllocationId);
            return acquireResponse;
        }
        catch (OperationCanceledException caught) when (cancellationToken.IsCancellationRequested)
        {
            result = "canceled";
            exception = caught;
            throw;
        }
        catch (Exception caught)
        {
            exception = caught;
            throw;
        }
        finally
        {
            CompleteStorageCall(
                AcquireOperation,
                stopwatch,
                response,
                result,
                exception,
                request.ClientId,
                resourcePoolId: request.ResourcePoolId);
        }
    }

    /// <inheritdoc />
    public async Task<ResourceReleaseResponse> ReleaseAsync(
        ReleaseResourceRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = StartStorageCall(ReleaseOperation);
        activity?.SetTag("allocation.id", request.AllocationId);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        Exception? exception = null;
        var result = "unknown";

        try
        {
            response = await _httpClient.PostAsJsonAsync(
                ReleaseOperation.StorageRoute,
                request,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                result = "denied";
                throw new AllocationNotFoundException(request.AllocationId);
            }

            if (!response.IsSuccessStatusCode)
            {
                result = GetFailureResult(response.StatusCode);
                throw await StorageApiResponseReader.CreateUnexpectedExceptionAsync(response, cancellationToken);
            }

            var releaseResponse = await StorageApiResponseReader.ReadRequiredAsync<ResourceReleaseResponse>(
                response,
                cancellationToken,
                "The storage API returned an empty resource release response.");

            result = releaseResponse.Released ? "released" : "already_released";
            return releaseResponse;
        }
        catch (OperationCanceledException caught) when (cancellationToken.IsCancellationRequested)
        {
            result = "canceled";
            exception = caught;
            throw;
        }
        catch (Exception caught)
        {
            exception = caught;
            throw;
        }
        finally
        {
            CompleteStorageCall(
                ReleaseOperation,
                stopwatch,
                response,
                result,
                exception,
                allocationId: request.AllocationId);
        }
    }

    private Activity? StartStorageCall(StorageClientOperation operation)
    {
        var activity = _metrics.ActivitySource.StartActivity(
            $"storage.client.{operation.Name}",
            ActivityKind.Client);

        activity?.SetTag("storage.route", operation.StorageRoute);
        activity?.SetTag("public.route", operation.PublicRoute);
        activity?.SetTag("http.request.method", operation.Method);
        return activity;
    }

    private void CompleteStorageCall(
        StorageClientOperation operation,
        Stopwatch stopwatch,
        HttpResponseMessage? response,
        string result,
        Exception? exception,
        string? clientId = null,
        string? serviceId = null,
        string? resourcePoolId = null,
        string? allocationId = null)
    {
        stopwatch.Stop();
        var finalResult = GetFinalResult(result, exception);
        var durationMs = stopwatch.Elapsed.TotalMilliseconds;

        if (response is not null)
        {
            Activity.Current?.SetTag("http.response.status_code", (int)response.StatusCode);
        }

        Activity.Current?.SetTag("operation.result", finalResult);
        Activity.Current?.SetTag("duration_ms", durationMs);
        SetExceptionTags(Activity.Current, finalResult, exception);
        RecordStorageClientDuration(operation, durationMs, finalResult, response, clientId, serviceId, resourcePoolId);
        LogStorageClientCompletion(operation, durationMs, finalResult, response, exception, clientId, serviceId, resourcePoolId, allocationId);
    }

    private void RecordStorageClientDuration(
        StorageClientOperation operation,
        double durationMs,
        string result,
        HttpResponseMessage? response,
        string? clientId,
        string? serviceId,
        string? resourcePoolId)
    {
        var tags = CreateMetricTags(operation, result, response, clientId, serviceId, resourcePoolId);
        _metrics.StorageClientCallDuration.Record(durationMs, tags);

        switch (operation.Name)
        {
            case "access_check":
                _metrics.AccessCheckDuration.Record(durationMs, tags);
                break;
            case "resource_acquire":
                _metrics.ResourceAcquireDuration.Record(durationMs, tags);
                break;
            case "resource_release":
                _metrics.ResourceReleaseDuration.Record(durationMs, tags);
                break;
        }
    }

    private void LogStorageClientCompletion(
        StorageClientOperation operation,
        double durationMs,
        string result,
        HttpResponseMessage? response,
        Exception? exception,
        string? clientId,
        string? serviceId,
        string? resourcePoolId,
        string? allocationId)
    {
        var extraData = new
        {
            Operation = operation.Name,
            operation.PublicRoute,
            operation.StorageRoute,
            StatusCode = response is null ? null : (int?)(int)response.StatusCode,
            DurationMs = durationMs,
            Result = result,
            ClientId = clientId,
            ServiceId = serviceId,
            ResourcePoolId = resourcePoolId,
            AllocationId = allocationId
        };

        if (result == "canceled")
        {
            _logger.Info("Storage API runtime call canceled", extraData);
            return;
        }

        if (exception is not null && result is not "denied")
        {
            _logger.Error("Storage API runtime call failed", extraData, exception);
            return;
        }

        if (durationMs >= SlowStorageCallThresholdMs)
        {
            _logger.Warn("Storage API runtime call completed slowly", extraData);
            return;
        }

        if (result == "denied")
        {
            _logger.Info("Storage API runtime call denied", extraData);
            return;
        }

        _logger.Debug("Storage API runtime call completed", extraData);
    }

    private static TagList CreateMetricTags(
        StorageClientOperation operation,
        string result,
        HttpResponseMessage? response,
        string? clientId,
        string? serviceId,
        string? resourcePoolId)
    {
        var tags = new TagList
        {
            { "operation", operation.Name },
            { "public_route", operation.PublicRoute },
            { "storage_route", operation.StorageRoute },
            { "status_code", response is null ? "none" : ((int)response.StatusCode).ToString() },
            { "result", result }
        };

        AddOptionalTag(ref tags, "client_id", clientId);
        AddOptionalTag(ref tags, "service_id", serviceId);
        AddOptionalTag(ref tags, "resource_pool_id", resourcePoolId);
        return tags;
    }

    private static void AddOptionalTag(ref TagList tags, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add(name, value);
        }
    }

    private static void SetExceptionTags(Activity? activity, string result, Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        activity?.SetTag("error.type", exception.GetType().Name);
        if (result is not "denied" and not "canceled")
        {
            activity?.SetStatus(ActivityStatusCode.Error);
        }
    }

    private static string GetFinalResult(string result, Exception? exception)
    {
        if (exception is null || result is "denied" or "failed" or "canceled")
        {
            return result;
        }

        return "exception";
    }

    private static string GetFailureResult(HttpStatusCode statusCode) =>
        (int)statusCode is >= 400 and < 500 ? "denied" : "failed";

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

    private sealed record StorageClientOperation(
        string Name,
        string Method,
        string PublicRoute,
        string StorageRoute);
}
