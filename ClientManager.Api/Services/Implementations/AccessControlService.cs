using System.Diagnostics;

using ClientManager.Api.Models.Enums;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils.Extensions;
using ClientManager.Api.Utils.Instrumentation;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Implementations;

/// <summary>
/// Evaluates deny-by-default access policies for clients against services.
/// Combines client configuration lookup with rate limit evaluation at all three scopes
/// (per-client-per-service, global aggregate, and per-client global). Records usage
/// events and emits OpenTelemetry metrics for every access decision.
/// </summary>
public class AccessControlService : IAccessControlService
{
    private readonly IAppLogger<AccessControlService> _logger;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IRateLimitService _rateLimitService;
    private readonly ClientManagerMetrics _metrics;
    private readonly IUsageRecorder _usageRecorder;

    /// <summary>
    /// Initializes a new instance of <see cref="AccessControlService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="clientConfigDatabase">Database for client configurations.</param>
    /// <param name="serviceRepository">Repository for service definitions.</param>
    /// <param name="rateLimitService">Service for evaluating rate limits.</param>
    /// <param name="metrics">The metrics instrumentation instance.</param>
    /// <param name="usageRecorder">The usage event recorder.</param>
    public AccessControlService(
        IAppLogger<AccessControlService> logger,
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        IRateLimitService rateLimitService,
        ClientManagerMetrics metrics,
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
        var config = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken) ?? throw new ClientNotFoundException(clientId);
        if (!config.IsEnabled)
        {
            _metrics.AccessDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId },
                { MetricTagKey.Reason.ToTagName(), ServiceAccessDenialReason.ClientDisabled.ToTagValue() }
            });
            _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
            throw new ClientDisabledException(clientId);
        }

        var service = await _serviceRepository.GetByIdAsync(serviceId, cancellationToken) ?? throw new ServiceNotFoundException(serviceId);
        if (!service.IsEnabled)
        {
            _metrics.AccessDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId },
                { MetricTagKey.Reason.ToTagName(), ServiceAccessDenialReason.ServiceDisabled.ToTagValue() }
            });
            _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
            throw new ServiceDisabledException(serviceId);
        }

        if (!config.Services.TryGetValue(serviceId, out var serviceSettings))
        {
            _metrics.AccessDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId },
                { MetricTagKey.Reason.ToTagName(), ServiceAccessDenialReason.NotConfigured.ToTagValue() }
            });
            _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
            throw new AccessNotConfiguredException(clientId, serviceId);
        }

        if (!serviceSettings.IsAllowed)
        {
            _metrics.AccessDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId },
                { MetricTagKey.Reason.ToTagName(), ServiceAccessDenialReason.NotAllowed.ToTagValue() }
            });
            _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
            throw new AccessDeniedException(clientId, serviceId);
        }

        // Check global service rate limit before per-client limits
        var globalResult = await _rateLimitService.CheckGlobalServiceLimitAsync(config, serviceId, cancellationToken);
        if (!globalResult.IsAllowed)
        {
            _metrics.AccessDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId },
                { MetricTagKey.Reason.ToTagName(), ServiceAccessDenialReason.GlobalRateLimited.ToTagValue() }
            });
            _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
            throw new GlobalServiceRateLimitExceededException(globalResult.RetryAfterSeconds);
        }

        // Check per-client rate limit (this increments the counter)
        var result = await _rateLimitService.CheckAndIncrementAsync(config, serviceId, cancellationToken);
        if (!result.IsAllowed)
        {
            _metrics.AccessDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId },
                { MetricTagKey.Reason.ToTagName(), ServiceAccessDenialReason.RateLimited.ToTagValue() }
            });
            _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Denied);
            throw new ClientRateLimitExceededException(result.RetryAfterSeconds);
        }

        _logger.Info("Access granted", new { ClientId = clientId, ServiceId = serviceId });

        _metrics.AccessGranted.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId }
        });
        _usageRecorder.RecordServiceRequest(clientId, serviceId, UsageEventType.Granted);

        return new AccessCheckResponse
        {
            ClientId = clientId,
            ServiceId = serviceId,
            RemainingRequests = result.RemainingRequests
        };
    }

    /// <inheritdoc />
    public async Task<ClientAccessibilityResponse> GetClientAccessibilityAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken) ?? throw new ClientNotFoundException(clientId);
        var services = await _serviceRepository.GetAllAsync(cancellationToken);

        var serviceAccessibilities = new List<ServiceAccessibility>();
        foreach (var service in services)
        {
            var hasAccess = config.Services.TryGetValue(service.Id, out var settings) && settings.IsAllowed && service.IsEnabled;

            var isRateLimited = false;
            int? remainingRequests = null;

            if (hasAccess)
            {
                var rateLimitStatus = await _rateLimitService.CheckWithoutIncrementAsync(clientId, service.Id, cancellationToken);
                isRateLimited = !rateLimitStatus.IsAllowed;
                remainingRequests = rateLimitStatus.RemainingRequests;
            }

            serviceAccessibilities.Add(new ServiceAccessibility
            {
                ServiceId = service.Id,
                HasAccess = hasAccess,
                IsCurrentlyRateLimited = isRateLimited,
                RemainingRequests = remainingRequests
            });
        }

        return new ClientAccessibilityResponse
        {
            ClientId = clientId,
            Services = serviceAccessibilities
        };
    }
}
