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
/// Manages resource pool slot acquisition, release, and TTL-based cleanup.
/// Enforces both system-wide and per-client slot caps, checks global resource pool rate
/// limits, and records usage events and OpenTelemetry metrics for every allocation decision.
/// </summary>
public class ResourceAllocationService : IResourceAllocationService
{
    private readonly IAppLogger<ResourceAllocationService> _logger;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IRateLimitService _rateLimitService;
    private readonly ClientManagerMetrics _metrics;
    private readonly IUsageRecorder _usageRecorder;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAllocationService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="poolRepository">Repository for resource pool definitions.</param>
    /// <param name="allocationDatabase">Database for resource allocation state.</param>
    /// <param name="clientConfigDatabase">Database for client configurations.</param>
    /// <param name="rateLimitService">Service for evaluating rate limits.</param>
    /// <param name="metrics">The metrics instrumentation instance.</param>
    /// <param name="usageRecorder">The usage event recorder.</param>
    public ResourceAllocationService(
        IAppLogger<ResourceAllocationService> logger,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IClientConfigurationDatabase clientConfigDatabase,
        IRateLimitService rateLimitService,
        ClientManagerMetrics metrics,
        IUsageRecorder usageRecorder)
    {
        _logger = logger;
        _poolRepository = poolRepository;
        _allocationDatabase = allocationDatabase;
        _clientConfigDatabase = clientConfigDatabase;
        _rateLimitService = rateLimitService;
        _metrics = metrics;
        _usageRecorder = usageRecorder;
    }

    /// <inheritdoc />
    public async Task<ResourceAcquireResponse> AcquireAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken = default)
    {
        var pool = await _poolRepository.GetByIdAsync(resourcePoolId, cancellationToken) ?? throw new ResourcePoolNotFoundException(resourcePoolId);
        var config = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken) ?? throw new ClientNotFoundException(clientId);
        if (!config.IsEnabled)
        {
            throw new ClientDisabledException(clientId);
        }

        // Check per-client pool quota if configured
        if (config.ResourcePools.TryGetValue(resourcePoolId, out var poolSettings))
        {
            var clientActiveCount = await _allocationDatabase.GetActiveCountByClientAsync(resourcePoolId, clientId, cancellationToken);
            if (clientActiveCount >= poolSettings.MaxSlots)
            {
                _metrics.ResourceDenied.Add(1, new TagList
                {
                    { MetricTagKey.ClientId.ToTagName(), clientId },
                    { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId },
                    { MetricTagKey.Reason.ToTagName(), ResourceAllocationDenialReason.ClientCapReached.ToTagValue() }
                });
                _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Denied);
                throw new ClientSlotLimitReachedException(resourcePoolId);
            }
        }

        // Check global resource pool rate limit
        var rateLimitResult = await _rateLimitService.CheckGlobalResourcePoolLimitAsync(config, resourcePoolId, cancellationToken);
        if (!rateLimitResult.IsAllowed)
        {
            _metrics.ResourceDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId },
                { MetricTagKey.Reason.ToTagName(), ResourceAllocationDenialReason.RateLimited.ToTagValue() }
            });
            _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Denied);
            throw new GlobalResourcePoolRateLimitExceededException(rateLimitResult.RetryAfterSeconds);
        }

        // Check system-wide pool capacity
        var activeCount = await _allocationDatabase.GetActiveCountAsync(resourcePoolId, cancellationToken);
        if (activeCount >= pool.MaxSlots)
        {
            _metrics.ResourceDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId },
                { MetricTagKey.Reason.ToTagName(), ResourceAllocationDenialReason.NoSlots.ToTagValue() }
            });
            _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Denied);
            throw new NoSlotsAvailableException(resourcePoolId);
        }

        var allocationId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var expiresAt = now + pool.AllocationTtl;

        var allocation = new ResourceAllocation
        {
            Id = allocationId,
            ResourcePoolId = resourcePoolId,
            ClientId = clientId,
            AcquiredAt = now,
            ExpiresAt = expiresAt,
            IsReleased = false
        };

        await _allocationDatabase.CreateAsync(allocation, cancellationToken);

        _metrics.ResourceAcquired.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId }
        });
        _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Granted);

        _logger.Info("Resource acquired", new { ClientId = clientId, ResourcePoolId = resourcePoolId, AllocationId = allocationId, ExpiresAt = expiresAt });

        return new ResourceAcquireResponse
        {
            AllocationId = allocationId,
            ExpiresAt = expiresAt
        };
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseAsync(string allocationId, CancellationToken cancellationToken = default)
    {
        var allocation = await _allocationDatabase.GetByIdAsync(allocationId, cancellationToken) ?? throw new AllocationNotFoundException(allocationId);
        if (allocation.IsReleased)
        {
            return false;
        }

        await _allocationDatabase.MarkReleasedAsync(allocationId, cancellationToken);

        _metrics.ResourceReleased.Add(1, new TagList
        {
            { MetricTagKey.AllocationId.ToTagName(), allocationId }
        });
        _usageRecorder.RecordAllocationEvent(allocation.ClientId, allocation.ResourcePoolId, UsageEventType.Released);

        _logger.Info("Resource released", new { AllocationId = allocationId });

        return true;
    }

    /// <inheritdoc />
    public async Task CleanupExpiredAllocationsAsync(CancellationToken cancellationToken = default)
    {
        var cleanedUp = await _allocationDatabase.CleanupExpiredAsync(cancellationToken);

        if (cleanedUp > 0)
        {
            _metrics.ResourceExpired.Add(cleanedUp);
            _logger.Info("Expired allocations cleaned up", new { Count = cleanedUp });
        }
    }
}
