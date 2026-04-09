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
        var configuration = await GetConfigurationAsync(clientId, serviceId, cancellationToken);
        var service = await GetServiceAsync(serviceId, clientId, cancellationToken);
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

        return new AccessCheckResponse
        {
            ClientId = clientId,
            ServiceId = service.Id,
            RemainingRequests = rateLimitResult.RemainingRequests
        };
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
        string serviceId,
        CancellationToken cancellationToken)
    {
        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ClientNotFoundException(clientId);

        if (configuration.IsEnabled)
        {
            return configuration;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.ClientDisabled);
        throw new ClientDisabledException(clientId);
    }

    private async Task<Service> GetServiceAsync(
        string serviceId,
        string clientId,
        CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken)
            ?? throw new ServiceNotFoundException(serviceId);

        if (service.IsEnabled)
        {
            return service;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.ServiceDisabled);
        throw new ServiceDisabledException(serviceId);
    }

    private ServiceAccessSettings GetServiceSettings(
        ClientConfiguration configuration,
        string clientId,
        string serviceId)
    {
        if (configuration.Services.TryGetValue(serviceId, out var settings))
        {
            return settings;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.NotConfigured);
        throw new AccessNotConfiguredException(clientId, serviceId);
    }

    private void EnsureServiceAccessAllowed(
        ServiceAccessSettings settings,
        string clientId,
        string serviceId)
    {
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
        var result = await _rateLimitService.CheckGlobalServiceLimitAsync(
            configuration,
            serviceId,
            cancellationToken);

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
        var result = await _rateLimitService.CheckAndIncrementAsync(
            configuration,
            serviceId,
            cancellationToken);

        if (result.IsAllowed)
        {
            return result;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.RateLimited);
        throw new ClientRateLimitExceededException(result.RetryAfterSeconds);
    }

    private void RecordGranted(string clientId, string serviceId)
    {
        _metrics.AccessGranted.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId }
        });

        _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Granted);
    }

    private void RecordDenied(
        string clientId,
        string serviceId,
        ServiceAccessDenialReason reason)
    {
        _metrics.AccessDenied.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId },
            { MetricTagKey.Reason.ToTagName(), reason.ToTagValue() }
        });

        _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
    }
}