using System.Diagnostics;
using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Models.Responses;
using ClientManager.Api.Services.Instrumentation;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services;

/// <summary>
/// Manages resource pool slot acquisition, release, and TTL-based cleanup.
/// Enforces both system-wide and per-client slot caps, and checks global resource pool rate limits.
/// </summary>
public class ResourceAllocationService : IResourceAllocationService
{
    private readonly IAppLogger<ResourceAllocationService> _logger;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationRepository _allocationRepository;
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IRateLimitService _rateLimitService;
    private readonly ClientManagerMetrics _metrics;
    private readonly IUsageRecorder _usageRecorder;

    /// <summary>
    /// Initializes a new instance of <see cref="ResourceAllocationService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="poolRepository">Repository for resource pool definitions.</param>
    /// <param name="allocationRepository">Repository for resource allocation state.</param>
    /// <param name="clientConfigRepository">Repository for client configurations.</param>
    /// <param name="rateLimitService">Service for evaluating rate limits.</param>
    /// <param name="metrics">The metrics instrumentation instance.</param>
    /// <param name="usageRecorder">The usage event recorder.</param>
    public ResourceAllocationService(
        IAppLogger<ResourceAllocationService> logger,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationRepository allocationRepository,
        IClientConfigurationRepository clientConfigRepository,
        IRateLimitService rateLimitService,
        ClientManagerMetrics metrics,
        IUsageRecorder usageRecorder)
    {
        _logger = logger;
        _poolRepository = poolRepository;
        _allocationRepository = allocationRepository;
        _clientConfigRepository = clientConfigRepository;
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
        var pool = await _poolRepository.GetByIdAsync(resourcePoolId, cancellationToken);
        if (pool is null)
        {
            throw new NotFoundException($"Resource pool '{resourcePoolId}' not found");
        }

        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            throw new NotFoundException($"Client '{clientId}' not found");
        }

        if (!config.IsEnabled)
        {
            throw new ClientDisabledException(clientId);
        }

        // Check per-client pool quota if configured
        if (config.ResourcePools.TryGetValue(resourcePoolId, out var poolSettings))
        {
            var clientActiveCount = await _allocationRepository.GetActiveCountByClientAsync(resourcePoolId, clientId, cancellationToken);
            if (clientActiveCount >= poolSettings.MaxSlots)
            {
                _metrics.ResourceDenied.Add(1, new TagList
                {
                    { MetricTagKey.ClientId.ToTagName(), clientId },
                    { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId },
                    { MetricTagKey.Reason.ToTagName(), ResourceDenialReason.ClientCapReached.ToTagValue() }
                });
                _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Denied);
                throw new RateLimitedException($"Client slot limit reached for pool '{resourcePoolId}'");
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
                { MetricTagKey.Reason.ToTagName(), ResourceDenialReason.RateLimited.ToTagValue() }
            });
            _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Denied);
            throw new RateLimitedException("Global resource pool rate limit exceeded", rateLimitResult.RetryAfterSeconds);
        }

        // Check system-wide pool capacity
        var activeCount = await _allocationRepository.GetActiveCountAsync(resourcePoolId, cancellationToken);
        if (activeCount >= pool.MaxSlots)
        {
            _metrics.ResourceDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId },
                { MetricTagKey.Reason.ToTagName(), ResourceDenialReason.NoSlots.ToTagValue() }
            });
            _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Denied);
            throw new RateLimitedException($"No slots available in pool '{resourcePoolId}'");
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

        await _allocationRepository.CreateAsync(allocation, cancellationToken);

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
        var allocation = await _allocationRepository.GetByIdAsync(allocationId, cancellationToken);
        if (allocation is null)
        {
            throw new NotFoundException($"Allocation '{allocationId}' not found");
        }

        if (allocation.IsReleased)
        {
            return false;
        }

        await _allocationRepository.MarkReleasedAsync(allocationId, cancellationToken);

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
        var cleanedUp = await _allocationRepository.CleanupExpiredAsync(cancellationToken);

        if (cleanedUp > 0)
        {
            _metrics.ResourceExpired.Add(cleanedUp);
            _logger.Info("Expired allocations cleaned up", new { Count = cleanedUp });
        }
    }
}
