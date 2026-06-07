using System.Diagnostics;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Extensions;
using ClientManager.Api.Services.Storage.Instrumentation;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Owns the deny-by-default access path in the API host.
/// </summary>
public class AccessControlService : IAccessControlService
{
    private const double SlowAccessCheckThresholdMs = 250;

    private readonly IAppLogger<AccessControlService> _logger;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IRateLimitService _rateLimitService;
    private readonly StorageMetrics _metrics;
    private readonly IUsageRecorder _usageRecorder;

    public AccessControlService(
        IAppLogger<AccessControlService> logger,
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        IRateLimitService rateLimitService,
        StorageMetrics metrics,
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
    public Task<AccessCheckResponse> CheckAccessAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default) =>
        StorageHotPathTrace.RunAsync(
            _metrics.ActivitySource,
            "storage.access.check",
            activity =>
            {
                activity?.SetTag("client.id", clientId);
                activity?.SetTag("service.id", serviceId);
            },
            async (completion, ct) =>
            {
                var configurationTask = ReadConfigurationAsync(clientId, ct);
                var serviceTask = ReadServiceAsync(serviceId, clientId, ct);
                ObserveFault(serviceTask);

                var configuration = await configurationTask;
                EnsureClientEnabled(configuration, clientId, serviceId);

                var service = await serviceTask;
                EnsureServiceEnabled(service, clientId);
                var serviceSettings = ReadServiceSettings(configuration, clientId, service.Id);

                EnsureServiceAccessAllowed(serviceSettings, clientId, service.Id);
                await EnsureGlobalLimitAsync(configuration, clientId, service.Id, ct);

                var rateLimitResult = await EnsureClientLimitAsync(
                    configuration,
                    clientId,
                    service.Id,
                    ct);

                _logger.Info("Access granted", new { ClientId = clientId, ServiceId = service.Id });
                RecordGranted(clientId, service.Id);
                completion.SetOutcome("granted", "Allowed");

                return new AccessCheckResponse
                {
                    ClientId = clientId,
                    ServiceId = service.Id,
                    RemainingRequests = rateLimitResult.RemainingRequests
                };
            },
            completion => RecordAccessCheckCompletion(clientId, serviceId, completion),
            cancellationToken);

    /// <inheritdoc />
    public async Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw DomainErrors.Client(clientId);

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

    private async Task<ClientConfiguration> ReadConfigurationAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.configuration_read",
            act => act?.SetTag("client.id", clientId));

        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw DomainErrors.Client(clientId);

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
        throw DomainErrors.ClientDisabled(clientId);
    }

    private async Task<Service> ReadServiceAsync(
        string serviceId,
        string clientId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.service_read",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
            });

        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken)
            ?? throw DomainErrors.Service(serviceId);

        activity?.SetTag("service.enabled", service.IsEnabled);
        return service;
    }

    private void EnsureServiceEnabled(Service service, string clientId)
    {
        if (service.IsEnabled)
        {
            return;
        }

        RecordDenied(clientId, service.Id, ServiceAccessDenialReason.ServiceDisabled);
        throw DomainErrors.ServiceDisabled(service.Id);
    }

    private ServiceAccessSettings ReadServiceSettings(
        ClientConfiguration configuration,
        string clientId,
        string serviceId)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.service_settings",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
            });

        if (configuration.Services.TryGetValue(serviceId, out var settings))
        {
            activity?.SetTag("access.configured", true);
            return settings;
        }

        activity?.SetTag("access.configured", false);
        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.NotConfigured);
        throw DomainErrors.AccessNotConfigured(clientId, serviceId);
    }

    private void EnsureServiceAccessAllowed(
        ServiceAccessSettings settings,
        string clientId,
        string serviceId)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.policy_check",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
                act?.SetTag("access.allowed", settings.IsAllowed);
            });

        if (settings.IsAllowed)
        {
            return;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.NotAllowed);
        throw DomainErrors.AccessDenied(clientId, serviceId);
    }

    private async Task EnsureGlobalLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.global_rate_limit",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
            });

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
        throw DomainErrors.GlobalServiceRateLimitExceeded(result.RetryAfterSeconds);
    }

    private async Task<RateLimitResult> EnsureClientLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.client_rate_limit",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
            });

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
        throw DomainErrors.ClientRateLimitExceeded(result.RetryAfterSeconds);
    }

    private void RecordGranted(string clientId, string serviceId)
    {
        using (var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.metrics",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
                act?.SetTag("operation.result", "granted");
            }))
        {
            _metrics.AccessGranted.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId }
            });
        }

        using var usageActivity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.usage_record",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
                act?.SetTag("usage.event_type", UsageEventType.Granted.ToString());
            });
        _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Granted);
    }

    private void RecordDenied(
        string clientId,
        string serviceId,
        ServiceAccessDenialReason reason)
    {
        using (var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.metrics",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
                act?.SetTag("denial.reason", reason.ToTagValue());
            }))
        {
            _metrics.AccessDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId },
                { MetricTagKey.Reason.ToTagName(), reason.ToTagValue() }
            });
        }

        using var usageActivity = _metrics.ActivitySource.StartInternalActivity(
            "storage.access.usage_record",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("service.id", serviceId);
                act?.SetTag("usage.event_type", UsageEventType.Denied.ToString());
            });
        _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
    }

    private void RecordAccessCheckCompletion(
        string clientId,
        string serviceId,
        StorageHotPathCompletion completion)
    {
        _metrics.AccessCheckDuration.Record(completion.DurationMs, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId },
            { "result", completion.Result },
            { "reason", completion.Reason }
        });

        var extraData = new
        {
            ClientId = clientId,
            ServiceId = serviceId,
            DurationMs = completion.DurationMs,
            Result = completion.Result,
            Reason = completion.Reason
        };

        if (completion.Result == "canceled")
        {
            _logger.Debug("Access check canceled", extraData);
            return;
        }

        if (completion.UnexpectedException is not null)
        {
            _logger.Error("Access check failed", extraData, completion.UnexpectedException);
            return;
        }

        if (completion.DurationMs >= SlowAccessCheckThresholdMs)
        {
            _logger.Warn("Access check completed slowly", extraData);
            return;
        }

        if (completion.Result == "denied")
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
