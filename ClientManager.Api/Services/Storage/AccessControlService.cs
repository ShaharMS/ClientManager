using System.Diagnostics;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Extensions;
using ClientManager.Api.Services.Storage.Instrumentation;
using ClientManager.Api.Services.Storage.RateLimiting;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Api.Storage.Repositories.Interfaces;
using ClientManager.Api.Utils.Instrumentation;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Owns the deny-by-default access path in the API host.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="IAccessControlService"/> by loading catalog data through the read cache,
/// evaluating rate limits, recording RPM, and emitting storage/client metrics. Typed exceptions
/// thrown here are translated by <c>ErrorHandlingMiddleware</c> into the nginx-compatible status and
/// problem-header contract.
/// </para>
/// </remarks>
public class AccessControlService : IAccessControlService
{
    private readonly IAppLogger<AccessControlService> _logger;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly RateLimitService _rateLimitService;
    private readonly RpmAccountingService _rpmAccounting;
    private readonly StorageMetrics _storageMetrics;
    private readonly ClientManagerMetrics _clientMetrics;

    public AccessControlService(
        IAppLogger<AccessControlService> logger,
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        RateLimitService rateLimitService,
        RpmAccountingService rpmAccounting,
        StorageMetrics storageMetrics,
        ClientManagerMetrics clientMetrics)
    {
        _logger = logger;
        _clientConfigDatabase = clientConfigDatabase;
        _serviceRepository = serviceRepository;
        _rateLimitService = rateLimitService;
        _rpmAccounting = rpmAccounting;
        _storageMetrics = storageMetrics;
        _clientMetrics = clientMetrics;
    }

    /// <inheritdoc />
    public Task<AccessCheckResponse> CheckAccessAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default) =>
        StorageHotPathTrace.RunAsync(
            _storageMetrics.ActivitySource,
            "storage.access.check",
            activity =>
            {
                activity?.SetTag("client.id", clientId);
                activity?.SetTag("service.id", serviceId);
            },
            async (completion, ct) =>
            {
                var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, ct);
                if (configuration is null)
                {
                    _clientMetrics.RecordAccessOutcome(serviceId, clientId, "client_not_found");
                    throw DomainErrors.UnknownClient(clientId);
                }
                EnsureClientEnabled(configuration, clientId, serviceId);

                var service = await _serviceRepository.GetByIdAsync(serviceId, ct);
                if (service is null)
                {
                    _clientMetrics.RecordAccessOutcome(serviceId, clientId, "service_not_found");
                    throw DomainErrors.Service(serviceId);
                }
                EnsureServiceEnabled(service, clientId);

                var serviceSettings = ReadServiceSettings(configuration, clientId, service.Id);
                EnsureServiceAccessAllowed(serviceSettings, clientId, service.Id);
                await EnsureGlobalLimitAsync(configuration, clientId, service.Id, ct);

                var rateLimitResult = await EnsureClientLimitAsync(configuration, clientId, service.Id, ct);

                _rpmAccounting.RecordRequest();
                _clientMetrics.RecordAccessOutcome(service.Id, clientId, "granted");
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

    private void EnsureClientEnabled(ClientConfiguration configuration, string clientId, string serviceId)
    {
        if (configuration.IsEnabled) return;
        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.ClientDisabled);
        throw DomainErrors.ClientDisabled(clientId);
    }

    private void EnsureServiceEnabled(Service service, string clientId)
    {
        if (service.IsEnabled) return;
        RecordDenied(clientId, service.Id, ServiceAccessDenialReason.ServiceDisabled);
        throw DomainErrors.ServiceDisabled(service.Id);
    }

    private ServiceAccessSettings ReadServiceSettings(ClientConfiguration configuration, string clientId, string serviceId)
    {
        if (configuration.Services.TryGetValue(serviceId, out var settings))
        {
            return settings;
        }

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.NotConfigured);
        throw DomainErrors.AccessNotConfigured(clientId, serviceId);
    }

    private void EnsureServiceAccessAllowed(ServiceAccessSettings settings, string clientId, string serviceId)
    {
        if (settings.IsAllowed) return;
        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.NotAllowed);
        throw DomainErrors.AccessDenied(clientId, serviceId);
    }

    private async Task EnsureGlobalLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var result = await _rateLimitService.CheckGlobalServiceLimitAsync(configuration, serviceId, cancellationToken);
        if (result.IsAllowed) return;

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.GlobalRateLimited);
        throw DomainErrors.GlobalServiceRateLimitExceeded(result.RetryAfterSeconds);
    }

    private async Task<RateLimitResult> EnsureClientLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var result = await _rateLimitService.CheckAndIncrementAsync(configuration, serviceId, cancellationToken);
        if (result.IsAllowed) return result;

        RecordDenied(clientId, serviceId, ServiceAccessDenialReason.RateLimited);
        throw DomainErrors.ClientRateLimitExceeded(result.RetryAfterSeconds);
    }

    private void RecordDenied(string clientId, string serviceId, ServiceAccessDenialReason reason)
    {
        _storageMetrics.AccessDenied.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId },
            { MetricTagKey.Reason.ToTagName(), reason.ToTagValue() }
        });
        _clientMetrics.RecordAccessOutcome(serviceId, clientId, reason.ToTagValue());
    }

    private void RecordAccessCheckCompletion(string clientId, string serviceId, StorageHotPathCompletion completion)
    {
        _storageMetrics.AccessCheckDuration.Record(completion.DurationMs, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId },
            { "result", completion.Result },
            { "reason", completion.Reason }
        });
    }
}
