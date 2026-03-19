using ClientManager.Api.Interfaces;
using ClientManager.Api.Models.Responses;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services;

/// <summary>
/// Provides aggregated statistics for the dashboard by reading from data stores
/// and computing usage metrics, time-series data, and client summaries.
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IResourceAllocationRepository _allocationRepository;
    private readonly IGlobalRateLimitRepository _globalRateLimitRepository;

    /// <summary>
    /// Initializes a new instance of <see cref="StatisticsService"/>.
    /// </summary>
    /// <param name="clientConfigRepository">Repository for client configurations.</param>
    /// <param name="serviceRepository">Repository for service definitions.</param>
    /// <param name="poolRepository">Repository for resource pool definitions.</param>
    /// <param name="allocationRepository">Repository for resource allocation state.</param>
    /// <param name="globalRateLimitRepository">Repository for global rate limits.</param>
    public StatisticsService(
        IClientConfigurationRepository clientConfigRepository,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IResourceAllocationRepository allocationRepository,
        IGlobalRateLimitRepository globalRateLimitRepository)
    {
        _clientConfigRepository = clientConfigRepository;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _allocationRepository = allocationRepository;
        _globalRateLimitRepository = globalRateLimitRepository;
    }

    /// <inheritdoc />
    public async Task<GlobalUsageStatsResponse> GetGlobalUsageStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var pools = await _poolRepository.GetAllAsync(cancellationToken);

        var totalSlots = 0;
        var acquiredSlots = 0;

        foreach (var pool in pools)
        {
            totalSlots += (int)pool.MaxSlots;
            acquiredSlots += await _allocationRepository.GetActiveCountAsync(pool.Id, cancellationToken);
        }

        var acquisitionPercentage = totalSlots > 0
            ? Math.Round(acquiredSlots * 100.0 / totalSlots, 1)
            : 0;

        // Request rate tracking is not yet persisted — return 0 for now.
        // A follow-up plan can add proper metrics storage.
        return new GlobalUsageStatsResponse(
            RequestsPerMinute: 0,
            TotalPoolSlots: totalSlots,
            AcquiredPoolSlots: acquiredSlots,
            AcquisitionPercentage: acquisitionPercentage);
    }

    /// <inheritdoc />
    public async Task<UsageTimeSeriesResponse> GetUsageTimeSeriesAsync(
        string filterType, string targetId, IEnumerable<string>? clientIds,
        CancellationToken cancellationToken = default)
    {
        // Historical request/allocation data is not yet persisted.
        // Return an empty series with a constant cap line based on current configuration.

        double capValue = 0;

        if (string.Equals(filterType, "Service", StringComparison.OrdinalIgnoreCase))
        {
            var globalLimit = await _globalRateLimitRepository.GetByTargetAsync(
                targetId, GlobalRateLimitTarget.Service, cancellationToken);
            capValue = globalLimit?.MaxRequests ?? 0;
        }
        else if (string.Equals(filterType, "ResourcePool", StringComparison.OrdinalIgnoreCase))
        {
            var pool = await _poolRepository.GetByIdAsync(targetId, cancellationToken);
            capValue = pool?.MaxSlots ?? 0;
        }

        // Generate empty time-series points for the last hour with the cap as a constant line
        var now = DateTime.UtcNow;
        var usagePoints = new List<TimeSeriesPoint>();
        var capPoints = new List<TimeSeriesPoint>();

        for (var i = 12; i >= 0; i--)
        {
            var timestamp = now.AddMinutes(-i * 5);
            usagePoints.Add(new TimeSeriesPoint(timestamp, 0));
            capPoints.Add(new TimeSeriesPoint(timestamp, capValue));
        }

        return new UsageTimeSeriesResponse(usagePoints, capPoints);
    }

    /// <inheritdoc />
    public async Task<ClientUsageBreakdownResponse> GetClientUsageBreakdownAsync(
        string filterType, string targetId, IEnumerable<string>? clientIds,
        CancellationToken cancellationToken = default)
    {
        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
        var clientIdSet = clientIds?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entries = new List<ClientUsageEntry>();

        if (string.Equals(filterType, "Service", StringComparison.OrdinalIgnoreCase))
        {
            // For services: show which clients have access (no historical request data yet)
            foreach (var client in clients)
            {
                if (clientIdSet is not null && !clientIdSet.Contains(client.Id))
                    continue;

                if (client.Services.TryGetValue(targetId, out var settings) && settings.IsAllowed)
                {
                    entries.Add(new ClientUsageEntry(client.Id, client.Name, 1));
                }
            }
        }
        else if (string.Equals(filterType, "ResourcePool", StringComparison.OrdinalIgnoreCase))
        {
            // For pools: show active slot allocations per client
            foreach (var client in clients)
            {
                if (clientIdSet is not null && !clientIdSet.Contains(client.Id))
                    continue;

                if (client.ResourcePools.ContainsKey(targetId))
                {
                    var activeCount = await _allocationRepository.GetActiveCountByClientAsync(
                        targetId, client.Id, cancellationToken);
                    entries.Add(new ClientUsageEntry(client.Id, client.Name, activeCount));
                }
            }
        }

        return new ClientUsageBreakdownResponse(entries);
    }

    /// <inheritdoc />
    public async Task<ClientSummariesResponse> GetClientSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var clients = await _clientConfigRepository.GetAllAsync(cancellationToken);
        var rows = new List<ClientSummaryRow>();

        foreach (var client in clients)
        {
            var accessibleServices = client.Services.Count(s => s.Value.IsAllowed);

            // Sum rate limit caps across all services that have one
            var totalMaxRequests = client.Services.Values
                .Where(s => s.RateLimit is not null)
                .Sum(s => s.RateLimit!.MaxRequests);

            var rateLimitCap = totalMaxRequests > 0
                ? $"{totalMaxRequests} req/min"
                : "—";

            var accessiblePools = client.ResourcePools.Count;

            var usedSlots = 0;
            var totalAccessibleSlots = 0;
            foreach (var (poolId, poolSettings) in client.ResourcePools)
            {
                totalAccessibleSlots += (int)poolSettings.MaxSlots;
                usedSlots += await _allocationRepository.GetActiveCountByClientAsync(
                    poolId, client.Id, cancellationToken);
            }

            rows.Add(new ClientSummaryRow(
                ClientId: client.Id,
                DisplayName: client.Name,
                AccessibleServices: accessibleServices,
                TotalRateLimitCap: rateLimitCap,
                AccessiblePools: accessiblePools,
                UsedSlots: usedSlots,
                TotalAccessibleSlots: totalAccessibleSlots));
        }

        return new ClientSummariesResponse(rows);
    }
}
