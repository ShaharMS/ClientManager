using System.Diagnostics;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.StorageApi.Models.Enums;
using ClientManager.StorageApi.Models.Exceptions;
using ClientManager.StorageApi.Services.Interfaces;
using ClientManager.StorageApi.Utils.Extensions;
using ClientManager.StorageApi.Utils.Instrumentation;

namespace ClientManager.StorageApi.Services.Implementations;

/// <summary>
/// Owns resource-slot acquisition, release, and cleanup inside the storage host.
/// </summary>
public class ResourceAllocationService : IResourceAllocationService
{
    private readonly IAppLogger<ResourceAllocationService> _logger;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IRateLimitService _rateLimitService;
    private readonly StorageApiMetrics _metrics;
    private readonly IUsageRecorder _usageRecorder;

    public ResourceAllocationService(
        IAppLogger<ResourceAllocationService> logger,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IClientConfigurationDatabase clientConfigDatabase,
        IRateLimitService rateLimitService,
        StorageApiMetrics metrics,
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
        var pool = await _poolRepository.GetByIdAsync(resourcePoolId, cancellationToken)
            ?? throw new ResourcePoolNotFoundException(resourcePoolId);

        var configuration = await GetConfigurationAsync(clientId, resourcePoolId, cancellationToken);

        await EnsureClientCapacityAsync(configuration, clientId, resourcePoolId, cancellationToken);
        await EnsureGlobalLimitAsync(configuration, clientId, resourcePoolId, cancellationToken);
        await EnsurePoolCapacityAsync(pool, clientId, resourcePoolId, cancellationToken);

        var allocation = CreateAllocation(clientId, resourcePoolId, pool.AllocationTtl);
        await _allocationDatabase.CreateAsync(allocation, cancellationToken);

        _logger.Info("Resource acquired", new
        {
            ClientId = clientId,
            ResourcePoolId = resourcePoolId,
            allocation.Id,
            allocation.ExpiresAt
        });

        RecordGranted(clientId, resourcePoolId);

        return new ResourceAcquireResponse
        {
            AllocationId = allocation.Id,
            ExpiresAt = allocation.ExpiresAt
        };
    }

    /// <inheritdoc />
    public async Task<ResourceReleaseResponse> ReleaseAsync(
        string allocationId,
        CancellationToken cancellationToken = default)
    {
        var allocation = await _allocationDatabase.GetByIdAsync(allocationId, cancellationToken)
            ?? throw new AllocationNotFoundException(allocationId);

        if (allocation.IsReleased)
        {
            return new ResourceReleaseResponse { Released = false };
        }

        await _allocationDatabase.MarkReleasedAsync(allocationId, cancellationToken);

        _metrics.ResourceReleased.Add(1, new TagList
        {
            { MetricTagKey.AllocationId.ToTagName(), allocationId }
        });

        _usageRecorder.RecordAllocationEvent(
            allocation.ClientId,
            allocation.ResourcePoolId,
            UsageEventType.Released);

        _logger.Info("Resource released", new { AllocationId = allocationId });

        return new ResourceReleaseResponse { Released = true };
    }

    /// <inheritdoc />
    public async Task CleanupExpiredAllocationsAsync(CancellationToken cancellationToken = default)
    {
        var cleanedUp = await _allocationDatabase.CleanupExpiredAsync(cancellationToken);
        if (cleanedUp <= 0)
        {
            return;
        }

        _metrics.ResourceExpired.Add(cleanedUp);
        _logger.Info("Expired allocations cleaned up", new { Count = cleanedUp });
    }

    private async Task<ClientConfiguration> GetConfigurationAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ClientNotFoundException(clientId);

        if (configuration.IsEnabled)
        {
            return configuration;
        }

        RecordDenied(clientId, resourcePoolId, ResourceAllocationDenialReason.ClientCapReached);
        throw new ClientDisabledException(clientId);
    }

    private async Task EnsureClientCapacityAsync(
        ClientConfiguration configuration,
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        if (!configuration.ResourcePools.TryGetValue(resourcePoolId, out var poolSettings))
        {
            return;
        }

        var clientActiveCount = await _allocationDatabase.GetActiveCountByClientAsync(
            resourcePoolId,
            clientId,
            cancellationToken);

        if (clientActiveCount < poolSettings.MaxSlots)
        {
            return;
        }

        RecordDenied(clientId, resourcePoolId, ResourceAllocationDenialReason.ClientCapReached);
        throw new ClientSlotLimitReachedException(resourcePoolId);
    }

    private async Task EnsureGlobalLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        var result = await _rateLimitService.CheckGlobalResourcePoolLimitAsync(
            configuration,
            resourcePoolId,
            cancellationToken);

        if (result.IsAllowed)
        {
            return;
        }

        RecordDenied(clientId, resourcePoolId, ResourceAllocationDenialReason.RateLimited);
        throw new GlobalResourcePoolRateLimitExceededException(result.RetryAfterSeconds);
    }

    private async Task EnsurePoolCapacityAsync(
        ResourcePool pool,
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        var activeCount = await _allocationDatabase.GetActiveCountAsync(resourcePoolId, cancellationToken);
        if (activeCount < pool.MaxSlots)
        {
            return;
        }

        RecordDenied(clientId, resourcePoolId, ResourceAllocationDenialReason.NoSlots);
        throw new NoSlotsAvailableException(resourcePoolId);
    }

    private static ResourceAllocation CreateAllocation(
        string clientId,
        string resourcePoolId,
        TimeSpan allocationTtl)
    {
        var now = DateTime.UtcNow;

        return new ResourceAllocation
        {
            Id = Guid.NewGuid().ToString(),
            ResourcePoolId = resourcePoolId,
            ClientId = clientId,
            AcquiredAt = now,
            ExpiresAt = now + allocationTtl,
            IsReleased = false
        };
    }

    private void RecordGranted(string clientId, string resourcePoolId)
    {
        _metrics.ResourceAcquired.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId }
        });

        _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Granted);
    }

    private void RecordDenied(
        string clientId,
        string resourcePoolId,
        ResourceAllocationDenialReason reason)
    {
        _metrics.ResourceDenied.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId },
            { MetricTagKey.Reason.ToTagName(), reason.ToTagValue() }
        });

        _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, UsageEventType.Denied);
    }
}