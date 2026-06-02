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
    private const double SlowResourceOperationThresholdMs = 250;

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
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.acquire",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("resource_pool.id", resourcePoolId);

        var stopwatch = Stopwatch.StartNew();
        var result = "unknown";
        var reason = "Unknown";
        Exception? unexpectedException = null;

        try
        {
            var poolTask = GetPoolAsync(resourcePoolId, cancellationToken);
            var configurationTask = GetConfigurationAsync(clientId, resourcePoolId, cancellationToken);
            ObserveFault(configurationTask);

            var pool = await poolTask;
            var configuration = await configurationTask;
            var activeCounts = await GetActiveCountsAsync(clientId, resourcePoolId, cancellationToken);

            EnsureClientCapacity(configuration, clientId, resourcePoolId, activeCounts.ClientCount);
            await EnsureGlobalLimitAsync(configuration, clientId, resourcePoolId, cancellationToken);
            EnsurePoolCapacity(pool, clientId, resourcePoolId, activeCounts.PoolCount);

            var allocation = CreateAllocation(clientId, resourcePoolId, pool.AllocationTtl);
            await WriteAllocationAsync(allocation, cancellationToken);

            _logger.Info("Resource acquired", new
            {
                ClientId = clientId,
                ResourcePoolId = resourcePoolId,
                allocation.Id,
                allocation.ExpiresAt
            });

            RecordGranted(clientId, resourcePoolId);
            result = "acquired";
            reason = "Allowed";
            activity?.SetTag("allocation.id", allocation.Id);

            return new ResourceAcquireResponse
            {
                AllocationId = allocation.Id,
                ExpiresAt = allocation.ExpiresAt
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
            RecordResourceDuration("acquire", clientId, resourcePoolId, durationMs, result, reason);
            LogResourceCompletion("acquire", clientId, resourcePoolId, null, durationMs, result, reason, unexpectedException);
        }
    }

    /// <inheritdoc />
    public async Task<ResourceReleaseResponse> ReleaseAsync(
        string allocationId,
        CancellationToken cancellationToken = default)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.release",
            ActivityKind.Internal);
        activity?.SetTag("allocation.id", allocationId);

        var stopwatch = Stopwatch.StartNew();
        ResourceAllocation? allocation = null;
        var result = "unknown";
        var reason = "Unknown";
        Exception? unexpectedException = null;

        try
        {
            allocation = await GetAllocationAsync(allocationId, cancellationToken);
            activity?.SetTag("client.id", allocation.ClientId);
            activity?.SetTag("resource_pool.id", allocation.ResourcePoolId);

            if (allocation.IsReleased)
            {
                result = "already_released";
                reason = "AlreadyReleased";
                return new ResourceReleaseResponse { Released = false };
            }

            await MarkReleasedAsync(allocation, cancellationToken);
            RecordReleased(allocation);
            _logger.Info("Resource released", new { AllocationId = allocationId });
            result = "released";
            reason = "Released";

            return new ResourceReleaseResponse { Released = true };
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
            RecordResourceDuration("release", allocation?.ClientId, allocation?.ResourcePoolId, durationMs, result, reason);
            LogResourceCompletion("release", allocation?.ClientId, allocation?.ResourcePoolId, allocationId, durationMs, result, reason, unexpectedException);
        }
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

    private async Task<ResourcePool> GetPoolAsync(
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.pool_read",
            ActivityKind.Internal);
        activity?.SetTag("resource_pool.id", resourcePoolId);

        var pool = await _poolRepository.GetByIdAsync(resourcePoolId, cancellationToken)
            ?? throw new ResourcePoolNotFoundException(resourcePoolId);

        activity?.SetTag("resource_pool.max_slots", pool.MaxSlots);
        return pool;
    }

    private async Task<ResourceAllocation> GetAllocationAsync(
        string allocationId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.allocation_read",
            ActivityKind.Internal);
        activity?.SetTag("allocation.id", allocationId);

        var allocation = await _allocationDatabase.GetByIdAsync(allocationId, cancellationToken)
            ?? throw new AllocationNotFoundException(allocationId);

        activity?.SetTag("allocation.released", allocation.IsReleased);
        return allocation;
    }

    private async Task<ClientConfiguration> GetConfigurationAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.configuration_read",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("resource_pool.id", resourcePoolId);

        var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ClientNotFoundException(clientId);

        activity?.SetTag("configuration.enabled", configuration.IsEnabled);

        if (configuration.IsEnabled)
        {
            return configuration;
        }

        throw new ClientDisabledException(clientId);
    }

    private async Task<(int PoolCount, int ClientCount)> GetActiveCountsAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.capacity_counts_read",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("resource_pool.id", resourcePoolId);

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
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.client_capacity_check",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("resource_pool.id", resourcePoolId);

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
        throw new ClientSlotLimitReachedException(resourcePoolId);
    }

    private async Task EnsureGlobalLimitAsync(
        ClientConfiguration configuration,
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.global_rate_limit",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("resource_pool.id", resourcePoolId);

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
        throw new GlobalResourcePoolRateLimitExceededException(result.RetryAfterSeconds);
    }

    private void EnsurePoolCapacity(
        ResourcePool pool,
        string clientId,
        string resourcePoolId,
        int activeCount)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.pool_capacity_check",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("resource_pool.id", resourcePoolId);
        activity?.SetTag("capacity.max_slots", pool.MaxSlots);

        activity?.SetTag("capacity.active_count", activeCount);
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

    private async Task WriteAllocationAsync(
        ResourceAllocation allocation,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.allocation_write",
            ActivityKind.Internal);
        activity?.SetTag("client.id", allocation.ClientId);
        activity?.SetTag("resource_pool.id", allocation.ResourcePoolId);
        activity?.SetTag("allocation.id", allocation.Id);

        await _allocationDatabase.CreateAsync(allocation, cancellationToken);
    }

    private async Task MarkReleasedAsync(
        ResourceAllocation allocation,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.release_write",
            ActivityKind.Internal);
        activity?.SetTag("allocation.id", allocation.Id);
        activity?.SetTag("client.id", allocation.ClientId);
        activity?.SetTag("resource_pool.id", allocation.ResourcePoolId);

        await _allocationDatabase.MarkReleasedAsync(allocation, cancellationToken);
    }

    private void RecordGranted(string clientId, string resourcePoolId)
    {
        using (var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.metrics",
            ActivityKind.Internal))
        {
            activity?.SetTag("client.id", clientId);
            activity?.SetTag("resource_pool.id", resourcePoolId);
            activity?.SetTag("operation.result", "acquired");
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
        using (var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.metrics",
            ActivityKind.Internal))
        {
            activity?.SetTag("client.id", clientId);
            activity?.SetTag("resource_pool.id", resourcePoolId);
            activity?.SetTag("denial.reason", reason.ToTagValue());
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
        using (var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.metrics",
            ActivityKind.Internal))
        {
            activity?.SetTag("client.id", allocation.ClientId);
            activity?.SetTag("resource_pool.id", allocation.ResourcePoolId);
            activity?.SetTag("allocation.id", allocation.Id);
            activity?.SetTag("operation.result", "released");
            _metrics.ResourceReleased.Add(1, new TagList
            {
                { MetricTagKey.AllocationId.ToTagName(), allocation.Id }
            });
        }

        RecordAllocationUsage(allocation.ClientId, allocation.ResourcePoolId, UsageEventType.Released);
    }

    private void RecordAllocationUsage(string clientId, string resourcePoolId, UsageEventType eventType)
    {
        using var activity = _metrics.ActivitySource.StartActivity(
            "storage.resource.usage_record",
            ActivityKind.Internal);
        activity?.SetTag("client.id", clientId);
        activity?.SetTag("resource_pool.id", resourcePoolId);
        activity?.SetTag("usage.event_type", eventType.ToString());
        _usageRecorder.RecordAllocationEvent(clientId, resourcePoolId, eventType);
    }

    private void RecordResourceDuration(
        string operation,
        string? clientId,
        string? resourcePoolId,
        double durationMs,
        string result,
        string reason)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "result", result },
            { "reason", reason }
        };
        AddOptionalTag(ref tags, MetricTagKey.ClientId.ToTagName(), clientId);
        AddOptionalTag(ref tags, MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId);

        if (operation == "acquire")
        {
            _metrics.ResourceAcquireDuration.Record(durationMs, tags);
            return;
        }

        _metrics.ResourceReleaseDuration.Record(durationMs, tags);
    }

    private void LogResourceCompletion(
        string operation,
        string? clientId,
        string? resourcePoolId,
        string? allocationId,
        double durationMs,
        string result,
        string reason,
        Exception? unexpectedException)
    {
        var extraData = new
        {
            Operation = operation,
            ClientId = clientId,
            ResourcePoolId = resourcePoolId,
            AllocationId = allocationId,
            DurationMs = durationMs,
            Result = result,
            Reason = reason
        };

        if (result == "canceled")
        {
            _logger.Debug("Resource operation canceled", extraData);
            return;
        }

        if (unexpectedException is not null)
        {
            _logger.Error("Resource operation failed", extraData, unexpectedException);
            return;
        }

        if (durationMs >= SlowResourceOperationThresholdMs)
        {
            _logger.Warn("Resource operation completed slowly", extraData);
            return;
        }

        if (result == "denied")
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