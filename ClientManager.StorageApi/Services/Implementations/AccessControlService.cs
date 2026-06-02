using System.Diagnostics;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Models.Entities;
using ClientManager.StorageApi.Models.Enums;
using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.StorageApi.Services.Interfaces;
using ClientManager.StorageApi.Utils.Extensions;
using ClientManager.StorageApi.Utils.Instrumentation;

namespace ClientManager.StorageApi.Services.Implementations;

/// <summary>
/// Owns the deny-by-default access path inside the storage-facing host.
/// </summary>
public class AccessControlService : IAccessControlService
{
    private const double SlowAccessCheckThresholdMs = 250;

    private readonly IAppLogger<AccessControlService> _logger;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IRateLimitService _rateLimitService;
    private readonly StorageApiMetrics _metrics;
    private readonly IUsageRecorder _usageRecorder;

    public AccessControlService(
        IAppLogger<AccessControlService> logger,
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        IRateLimitService rateLimitService,
        StorageApiMetrics metrics,
        IUsageRecorder usageRecorder)
    {
        _logger = logger;
        _clientConfigDatabase = clientConfigDatabase;
        _serviceRepository = serviceRepository;
        _rateLimitService = rateLimitService;
        _metrics = metrics;
        _usageRecorder = usageRecorder;
    }

    /// <inheritdoc />
    public async Task<AccessCheckResponse> CheckAccessAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.check",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("service.id", serviceId);

        var stopwatch = Stopwatch.StartNew();
        var result = "unknown";
        var reason = "Unknown";
        Exception? unexpectedException = null;

        try
        {
            var configurationTask = GetConfigurationAsync(clientId, cancellationToken);
            var serviceTask = GetServiceAsync(serviceId, clientId, cancellationToken);
            ObserveFault(serviceTask);

            var configuration = await configurationTask;
            EnsureClientEnabled(configuration, clientId, serviceId);

            var service = await serviceTask;
            EnsureServiceEnabled(service, clientId);
            var serviceSettings = GetServiceSettings(configuration, clientId, service.Id);

            EnsureServiceAccessAllowed(serviceSettings, clientId, service.Id);
            await EnsureGlobalLimitAsync(configuration, clientId, service.Id, cancellationToken);

            var rateLimitResult = await EnsureClientLimitAsync(
                configuration,
                clientId,
                service.Id,
                cancellationToken);

            _logger.Info("Access granted", new { ClientId = clientId, ServiceId = service.Id });
            RecordGranted(clientId, service.Id);
            result = "granted";
            reason = "Allowed";

            return new AccessCheckResponse
            {
                ClientId = clientId,
                ServiceId = service.Id,
                RemainingRequests = rateLimitResult.RemainingRequests
            };
        }
        catch (StorageApiProblemException exception)
        {
            result = "denied";
            reason = exception.ErrorCode;
            throw;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            result = "canceled";
            reason = exception.GetType().Name;
            unexpectedException = exception;
            throw;
        }
        catch (Exception exception)
        {
            result = "exception";
            reason = exception.GetType().Name;
            unexpectedException = exception;
            activity?.SetTag("error.type", exception.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            activity?.SetTag("operation.result", result);
            activity?.SetTag("denial.reason", reason);
            activity?.SetTag("duration_ms", durationMs);
            RecordAccessCheckDuration(clientId, serviceId, durationMs, result, reason);
            LogAccessCheckCompletion(clientId, serviceId, durationMs, result, reason, unexpectedException);
        }
    }

    /// <inheritdoc />
    public async Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ClientNotFoundException(clientId);

        var services = await _serviceRepository.GetAllAsync(cancellationToken);
        var accessibilities = new List<ServiceAccessibility>(services.Count);

        foreach (var service in services)
        {
            var hasAccess = configuration.Services.TryGetValue(service.Id, out var settings)
                && settings.IsAllowed
                && service.IsEnabled;

            var rateLimitResult = hasAccess
                ? await _rateLimitService.CheckWithoutIncrementAsync(clientId, service.Id, cancellationToken)
                : null;

            accessibilities.Add(new ServiceAccessibility
            {
                ServiceId = service.Id,
                HasAccess = hasAccess,
                IsCurrentlyRateLimited = rateLimitResult is not null && !rateLimitResult.IsAllowed,
                RemainingRequests = rateLimitResult?.RemainingRequests
            });
        }

        return new ClientAccessibilityResponse
        {
            ClientId = clientId,
            Services = accessibilities
        };
    }

    private async Task<ClientConfiguration> GetConfigurationAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.configuration_read",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);

        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ClientNotFoundException(clientId);

        activity?.SetTag("configuration.enabled", configuration.IsEnabled);
        return configuration;
    }

    private void EnsureClientEnabled(
        ClientConfiguration configuration,
        string clientId,
        string serviceId)
    {
        if (configuration.IsEnabled)
        {
            return;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.ClientDisabled);
        throw new ClientDisabledException(clientId);
    }

    private async Task<Service> GetServiceAsync(
        string serviceId,
        string clientId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.service_read",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("service.id", serviceId);

        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken)
            ?? throw new ServiceNotFoundException(serviceId);

        activity?.SetTag("service.enabled", service.IsEnabled);
        return service;
    }

    private void EnsureServiceEnabled(
        Service service,
        string clientId)
    {
        if (service.IsEnabled)
        {
            return;
        }

        RecordDenied(clientId, service.Id, ServiceAccessDenialReason.ServiceDisabled);
        throw new ServiceDisabledException(service.Id);
    }

    private ServiceAccessSettings GetServiceSettings(
        ClientConfiguration configuration,
        string clientId,
        string serviceId)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.service_settings",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("service.id", serviceId);

        if (configuration.Services.TryGetValue(serviceId, out var settings))
        {
            activity?.SetTag("access.configured", true);
            return settings;
        }

        activity?.SetTag("access.configured", false);
        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.NotConfigured);
        throw new AccessNotConfiguredException(clientId, serviceId);
    }

    private void EnsureServiceAccessAllowed(
        ServiceAccessSettings settings,
        string clientId,
        string serviceId)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.policy_check",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("service.id", serviceId);
        activity?.SetTag("access.allowed", settings.IsAllowed);

        if (settings.IsAllowed)
        {
            return;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.NotAllowed);
        throw new AccessDeniedException(clientId, serviceId);
    }

    private async Task EnsureGlobalLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.global_rate_limit",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("service.id", serviceId);

        var result = await _rateLimitService.CheckGlobalServiceLimitAsync(
            configuration,
            serviceId,
            cancellationToken);

        activity?.SetTag("ratelimit.result", result.IsAllowed ? "allowed" : "denied");
        activity?.SetTag("ratelimit.remaining_requests", result.RemainingRequests);

        if (result.IsAllowed)
        {
            return;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.GlobalRateLimited);
        throw new GlobalServiceRateLimitExceededException(result.RetryAfterSeconds);
    }

    private async Task<RateLimitResult> EnsureClientLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.client_rate_limit",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("service.id", serviceId);

        var result = await _rateLimitService.CheckAndIncrementAsync(
            configuration,
            serviceId,
            cancellationToken);

        activity?.SetTag("ratelimit.result", result.IsAllowed ? "allowed" : "denied");
        activity?.SetTag("ratelimit.remaining_requests", result.RemainingRequests);

        if (result.IsAllowed)
        {
            return result;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.RateLimited);
        throw new ClientRateLimitExceededException(result.RetryAfterSeconds);
    }

    private void RecordGranted(string clientId, string serviceId)
    {
        using (var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.metrics",
            ActivityKind.Internal))
        {
            activity?.SetTag("client.id", clientId);
            activity?.SetTag("service.id", serviceId);
            activity?.SetTag("operation.result", "granted");
            _metrics.AccessGranted.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId }
            });
        }

        using var usageActivity = _metrics.ActivitySource.StartActivity(
            "storage.access.usage_record",
            ActivityKind.Internal);
        usageActivity?.SetTag("client.id", clientId);
        usageActivity?.SetTag("service.id", serviceId);
        usageActivity?.SetTag("usage.event_type", UsageEventType.Granted.ToString());
        _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Granted);
    }

    private void RecordDenied(
        string clientId,
        string serviceId,
        ServiceAccessDenialReason reason)
    {
        using (var activity = _metrics.ActivitySource.StartActivity(
            "storage.access.metrics",
            ActivityKind.Internal))
        {
            activity?.SetTag("client.id", clientId);
            activity?.SetTag("service.id", serviceId);
            activity?.SetTag("denial.reason", reason.ToTagValue());
            _metrics.AccessDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId },
                { MetricTagKey.Reason.ToTagName(), reason.ToTagValue() }
            });
        }

        using var usageActivity = _metrics.ActivitySource.StartActivity(
            "storage.access.usage_record",
            ActivityKind.Internal);
        usageActivity?.SetTag("client.id", clientId);
        usageActivity?.SetTag("service.id", serviceId);
        usageActivity?.SetTag("usage.event_type", UsageEventType.Denied.ToString());
        _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
    }

    private void RecordAccessCheckDuration(
        string clientId,
        string serviceId,
        double durationMs,
        string result,
        string reason)
    {
        _metrics.AccessCheckDuration.Record(durationMs, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId },
            { "result", result },
            { "reason", reason }
        });
    }

    private void LogAccessCheckCompletion(
        string clientId,
        string serviceId,
        double durationMs,
        string result,
        string reason,
        Exception? unexpectedException)
    {
        var extraData = new
        {
            ClientId = clientId,
            ServiceId = serviceId,
            DurationMs = durationMs,
            Result = result,
            Reason = reason
        };

        if (result == "canceled")
        {
            _logger.Debug("Access check canceled", extraData);
            return;
        }

        if (unexpectedException is not null)
        {
            _logger.Error("Access check failed", extraData, unexpectedException);
            return;
        }

        if (durationMs >= SlowAccessCheckThresholdMs)
        {
            _logger.Warn("Access check completed slowly", extraData);
            return;
        }

        if (result == "denied")
        {
            _logger.Info("Access check denied", extraData);
            return;
        }

        _logger.Debug("Access check completed", extraData);
    }

    private static void ObserveFault<T>(Task<T> task)
    {
        _ = task.ContinueWith(
            completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}