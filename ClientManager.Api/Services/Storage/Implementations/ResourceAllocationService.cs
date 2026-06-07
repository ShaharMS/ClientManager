using System.Diagnostics;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;
using ClientManager.Api.Services.Storage.Models.Enums;
using ClientManager.Api.Models.Exceptions;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Utils.Extensions;
using ClientManager.Api.Services.Storage.Utils.Instrumentation;

namespace ClientManager.Api.Services.Storage.Implementations;

/// <summary>
/// Owns resource-slot acquisition, release, and cleanup inside the storage host.
/// </summary>
public class ResourceAllocationService : IResourceAllocationService
{
    private const double SlowResourceOperationThresholdMs = 250;

    private readonly IAppLogger<ResourceAllocationService> _logger;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationDatabase _allocationDatabase;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IRateLimitService _rateLimitService;
    private readonly StorageMetrics _metrics;
    private readonly IUsageRecorder _usageRecorder;

    public ResourceAllocationService(
        IAppLogger<ResourceAllocationService> logger,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationDatabase allocationDatabase,
        IClientConfigurationDatabase clientConfigDatabase,
        IRateLimitService rateLimitService,
        StorageMetrics metrics,
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
    public Task<ResourceAcquireResponse> AcquireAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken = default) =>
        StorageHotPathTrace.RunAsync(
            _metrics.ActivitySource,
            "storage.resource.acquire",
            activity =>
            {
                activity?.SetTag("client.id", clientId);
                activity?.SetTag("resource_pool.id", resourcePoolId);
            },
            async (completion, ct) =>
            {
                var poolTask = ReadPoolAsync(resourcePoolId, ct);
                var configurationTask = ReadConfigurationAsync(clientId, resourcePoolId, ct);
                ObserveFault(configurationTask);

                var pool = await poolTask;
                var configuration = await configurationTask;
                var activeCounts = await ReadActiveCountsAsync(clientId, resourcePoolId, ct);

                EnsureClientCapacity(configuration, clientId, resourcePoolId, activeCounts.ClientCount);
                await EnsureGlobalLimitAsync(configuration, clientId, resourcePoolId, ct);
                EnsurePoolCapacity(pool, clientId, resourcePoolId, activeCounts.PoolCount);

                var allocation = CreateAllocation(clientId, resourcePoolId, pool.AllocationTtl);
                await WriteAllocationAsync(allocation, ct);

                _logger.Info("Resource acquired", new
                {
                    ClientId = clientId,
                    ResourcePoolId = resourcePoolId,
                    allocation.Id,
                    allocation.ExpiresAt
                });

                RecordGranted(clientId, resourcePoolId);
                completion.SetOutcome("acquired", "Allowed");
                completion.Activity?.SetTag("allocation.id", allocation.Id);

                return new ResourceAcquireResponse
                {
                    AllocationId = allocation.Id,
                    ExpiresAt = allocation.ExpiresAt
                };
            },
            completion => RecordResourceCompletion("acquire", clientId, resourcePoolId, null, completion),
            cancellationToken);

    /// <inheritdoc />
    public Task<ResourceReleaseResponse> ReleaseAsync(
        string allocationId,
        CancellationToken cancellationToken = default)
    {
        string? clientId = null;
        string? resourcePoolId = null;

        return StorageHotPathTrace.RunAsync(
            _metrics.ActivitySource,
            "storage.resource.release",
            activity => activity?.SetTag("allocation.id", allocationId),
            async (completion, ct) =>
            {
                var allocation = await ReadAllocationAsync(allocationId, ct);
                clientId = allocation.ClientId;
                resourcePoolId = allocation.ResourcePoolId;
                completion.Activity?.SetTag("client.id", clientId);
                completion.Activity?.SetTag("resource_pool.id", resourcePoolId);

                if (allocation.IsReleased)
                {
                    completion.SetOutcome("already_released", "AlreadyReleased");
                    return new ResourceReleaseResponse { Released = false };
                }

                await MarkReleasedAsync(allocation, ct);
                RecordReleased(allocation);
                _logger.Info("Resource released", new { AllocationId = allocationId });
                completion.SetOutcome("released", "Released");

                return new ResourceReleaseResponse { Released = true };
            },
            completion => RecordResourceCompletion("release", clientId, resourcePoolId, allocationId, completion),
            cancellationToken);
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

    private async Task<ResourcePool> ReadPoolAsync(
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.pool_read",
            act => act?.SetTag("resource_pool.id", resourcePoolId));

        var pool = await _poolRepository.GetByIdAsync(resourcePoolId, cancellationToken)
            ?? throw DomainErrors.ResourcePool(resourcePoolId);

        activity?.SetTag("resource_pool.max_slots", pool.MaxSlots);
        return pool;
    }

    private async Task<ResourceAllocation> ReadAllocationAsync(
        string allocationId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.allocation_read",
            act => act?.SetTag("allocation.id", allocationId));

        var allocation = await _allocationDatabase.GetByIdAsync(allocationId, cancellationToken)
            ?? throw DomainErrors.Allocation(allocationId);

        activity?.SetTag("allocation.released", allocation.IsReleased);
        return allocation;
    }

    private async Task<ClientConfiguration> ReadConfigurationAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.configuration_read",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("resource_pool.id", resourcePoolId);
            });

        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw DomainErrors.Client(clientId);

        activity?.SetTag("configuration.enabled", configuration.IsEnabled);

        if (configuration.IsEnabled)
        {
            return configuration;
        }

        throw DomainErrors.ClientDisabled(clientId);
    }

    private async Task<(int PoolCount, int ClientCount)> ReadActiveCountsAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.capacity_counts_read",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("resource_pool.id", resourcePoolId);
            });

        var counts = await _allocationDatabase.GetActiveCountsAsync(resourcePoolId, clientId, cancellationToken);
        activity?.SetTag("capacity.pool_active_count", counts.PoolCount);
        activity?.SetTag("capacity.client_active_count", counts.ClientCount);
        return counts;
    }

    private void EnsureClientCapacity(
        ClientConfiguration configuration,
        string clientId,
        string resourcePoolId,
        int clientActiveCount)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.client_capacity_check",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("resource_pool.id", resourcePoolId);
            });

        if (!configuration.ResourcePools.TryGetValue(resourcePoolId, out var poolSettings))
        {
            activity?.SetTag("capacity.configured", false);
            return;
        }

        activity?.SetTag("capacity.configured", true);
        activity?.SetTag("capacity.max_slots", poolSettings.MaxSlots);
        activity?.SetTag("capacity.active_count", clientActiveCount);

        if (clientActiveCount < poolSettings.MaxSlots)
        {
            return;
        }

        RecordDenied(clientId, resourcePoolId, ResourceAllocationDenialReason.ClientCapReached);
        throw DomainErrors.ClientSlotLimitReached(resourcePoolId);
    }

    private async Task EnsureGlobalLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.global_rate_limit",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("resource_pool.id", resourcePoolId);
            });

        var result = await _rateLimitService.CheckGlobalResourcePoolLimitAsync(
            configuration,
            resourcePoolId,
            cancellationToken);

        activity?.SetTag("ratelimit.result", result.IsAllowed ? "allowed" : "denied");
        activity?.SetTag("ratelimit.remaining_requests", result.RemainingRequests);

        if (result.IsAllowed)
        {
            return;
        }

        RecordDenied(clientId, resourcePoolId, ResourceAllocationDenialReason.RateLimited);
        throw DomainErrors.GlobalResourcePoolRateLimitExceeded(result.RetryAfterSeconds);
    }

    private void EnsurePoolCapacity(
        ResourcePool pool,
        string clientId,
        string resourcePoolId,
        int activeCount)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.pool_capacity_check",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("resource_pool.id", resourcePoolId);
                act?.SetTag("capacity.max_slots", pool.MaxSlots);
            });

        activity?.SetTag("capacity.active_count", activeCount);
        if (activeCount < pool.MaxSlots)
        {
            return;
        }

        RecordDenied(clientId, resourcePoolId, ResourceAllocationDenialReason.NoSlots);
        throw DomainErrors.NoSlotsAvailable(resourcePoolId);
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

    private async Task WriteAllocationAsync(
        ResourceAllocation allocation,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.allocation_write",
            act =>
            {
                act?.SetTag("client.id", allocation.ClientId);
                act?.SetTag("resource_pool.id", allocation.ResourcePoolId);
                act?.SetTag("allocation.id", allocation.Id);
            });

        await _allocationDatabase.CreateAsync(allocation, cancellationToken);
    }

    private async Task MarkReleasedAsync(
        ResourceAllocation allocation,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.release_write",
            act =>
            {
                act?.SetTag("allocation.id", allocation.Id);
                act?.SetTag("client.id", allocation.ClientId);
                act?.SetTag("resource_pool.id", allocation.ResourcePoolId);
            });

        await _allocationDatabase.MarkReleasedAsync(allocation, cancellationToken);
    }

    private void RecordGranted(string clientId, string resourcePoolId)
    {
        using (var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.metrics",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("resource_pool.id", resourcePoolId);
                act?.SetTag("operation.result", "acquired");
            }))
        {
            _metrics.ResourceAcquired.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId }
            });
        }

        RecordAllocationUsage(clientId, resourcePoolId, UsageEventType.Granted);
    }

    private void RecordDenied(
        string clientId,
        string resourcePoolId,
        ResourceAllocationDenialReason reason)
    {
        using (var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.metrics",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("resource_pool.id", resourcePoolId);
                act?.SetTag("denial.reason", reason.ToTagValue());
            }))
        {
            _metrics.ResourceDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId },
                { MetricTagKey.Reason.ToTagName(), reason.ToTagValue() }
            });
        }

        RecordAllocationUsage(clientId, resourcePoolId, UsageEventType.Denied);
    }

    private void RecordReleased(ResourceAllocation allocation)
    {
        using (var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.metrics",
            act =>
            {
                act?.SetTag("client.id", allocation.ClientId);
                act?.SetTag("resource_pool.id", allocation.ResourcePoolId);
                act?.SetTag("allocation.id", allocation.Id);
                act?.SetTag("operation.result", "released");
            }))
        {
            _metrics.ResourceReleased.Add(1, new TagList
            {
                { MetricTagKey.AllocationId.ToTagName(), allocation.Id }
            });
        }

        RecordAllocationUsage(allocation.ClientId, allocation.ResourcePoolId, UsageEventType.Released);
    }

    private void RecordAllocationUsage(string clientId, string resourcePoolId, UsageEventType eventType)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.resource.usage_record",
            act =>
            {
                act?.SetTag("client.id", clientId);
                act?.SetTag("resource_pool.id", resourcePoolId);
                act?.SetTag("usage.event_type", eventType.ToString());
            });
        _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, eventType);
    }

    private void RecordResourceCompletion(
        string operation,
        string? clientId,
        string? resourcePoolId,
        string? allocationId,
        StorageHotPathCompletion completion)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "result", completion.Result },
            { "reason", completion.Reason }
        };
        AddOptionalTag(ref tags, MetricTagKey.ClientId.ToTagName(), clientId);
        AddOptionalTag(ref tags, MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId);

        if (operation == "acquire")
        {
            _metrics.ResourceAcquireDuration.Record(completion.DurationMs, tags);
        }
        else
        {
            _metrics.ResourceReleaseDuration.Record(completion.DurationMs, tags);
        }

        var extraData = new
        {
            Operation = operation,
            ClientId = clientId,
            ResourcePoolId = resourcePoolId,
            AllocationId = allocationId,
            DurationMs = completion.DurationMs,
            Result = completion.Result,
            Reason = completion.Reason
        };

        if (completion.Result == "canceled")
        {
            _logger.Debug("Resource operation canceled", extraData);
            return;
        }

        if (completion.UnexpectedException is not null)
        {
            _logger.Error("Resource operation failed", extraData, completion.UnexpectedException);
            return;
        }

        if (completion.DurationMs >= SlowResourceOperationThresholdMs)
        {
            _logger.Warn("Resource operation completed slowly", extraData);
            return;
        }

        if (completion.Result == "denied")
        {
            _logger.Info("Resource operation denied", extraData);
            return;
        }

        _logger.Debug("Resource operation completed", extraData);
    }

    private static void AddOptionalTag(ref TagList tags, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add(name, value);
        }
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
