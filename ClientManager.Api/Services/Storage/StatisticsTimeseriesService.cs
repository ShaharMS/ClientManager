using ClientManager.DataAccess.Databases.Implementations;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.DataAccess.Repositories.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Requests;
using ClientManager.Shared.Models.Responses;
using ClientManager.Shared.Models.Search;
using ClientManager.Shared.Utils;
using ClientManager.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Serves chart-ready statistics timeseries for <c>POST /statistics/timeseries/search</c>.
/// </summary>
/// <remarks>
/// <para>
/// Reads are intentionally split into a <strong>closed base</strong> and an optional
/// <strong>live overlay</strong> so dashboard polls stay fast while usage counters flush every second:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <strong>Closed base</strong> — snapshot aggregates for the requested range, cached via
/// <see cref="IStorageReadCache.GetOrCreateStatisticsClosedAsync{T}"/>. The cache key omits
/// wall-clock <c>toUtc</c> when the query includes the live tail so consecutive UI polls reuse
/// the same entry. Invalidated only when rollup/prune mutates closed history (slow persistence loop).
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Live overlay</strong> — per-request merge of pending second-level counters from one
/// batched <c>usage:</c> prefix read. Applied when <see cref="IncludesNow"/> is true (~2s of UTC now).
/// Tail freshness does not depend on per-flush cache busting.
/// </description>
/// </item>
/// </list>
/// <para>
/// After overlay merge, <see cref="StatisticsBucketMerger"/> produces display buckets for the response.
/// </para>
/// </remarks>
public sealed class StatisticsTimeseriesService : IStatisticsTimeseriesService
{
    private const int MinBucketCount = 5;
    private const int MaxBucketCount = 20;

    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IEntityRepository<Service> _serviceRepository;
    private readonly IEntityRepository<ResourcePool> _poolRepository;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly IUsageSnapshotDatabase _usageSnapshotDatabase;
    private readonly IStorageReadCache _cache;
    private readonly UsageTrackingOptions _usageTrackingOptions;

    public StatisticsTimeseriesService(
        IClientConfigurationDatabase clientConfigDatabase,
        IEntityRepository<Service> serviceRepository,
        IEntityRepository<ResourcePool> poolRepository,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        IUsageSnapshotDatabase usageSnapshotDatabase,
        IStorageReadCache cache,
        IOptions<UsageTrackingOptions> usageTrackingOptions)
    {
        _clientConfigDatabase = clientConfigDatabase;
        _serviceRepository = serviceRepository;
        _poolRepository = poolRepository;
        _globalRateLimitDatabase = globalRateLimitDatabase;
        _usageSnapshotDatabase = usageSnapshotDatabase;
        _cache = cache;
        _usageTrackingOptions = usageTrackingOptions.Value;
    }

    /// <inheritdoc />
    public Task<TimeseriesSearchResponse> SearchAsync(
        TimeseriesSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var bucketCount = Math.Clamp(request.BucketCount, MinBucketCount, MaxBucketCount);
        return BuildResponseAsync(request with { BucketCount = bucketCount }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<double> ComputeServiceRequestsPerMinuteAsync(CancellationToken cancellationToken = default)
    {
        const int windowMinutes = 5;
        var now = DateTime.UtcNow;
        var fromUtc = now.AddMinutes(-windowMinutes);
        const int bucketCount = 5;

        var targetIds = await ResolveTargetIdsAsync(StatisticsSearchCategory.ServiceRequests, null, cancellationToken);
        var clientIds = await ResolveClientIdsAsync(null, targetIds, TargetType.Service, cancellationToken);
        var granularity = StatisticsGranularityPicker.PickForRange(fromUtc, now, bucketCount);
        var snapshots = targetIds.Count == 0 || clientIds.Count == 0
            ? []
            : await _usageSnapshotDatabase.GetByTargetsAndRangeAsync(
                targetIds,
                TargetType.Service,
                granularity,
                fromUtc,
                now,
                clientIds,
                cancellationToken);

        var totals = AggregateSnapshots(snapshots, fromUtc, now, granularity);
        await OverlayLiveCountersBatchedAsync(
            totals,
            TargetType.Service,
            targetIds,
            clientIds,
            fromUtc,
            now,
            granularity,
            cancellationToken);

        return StatisticsRequestsPerMinuteCalculator.Compute(totals, fromUtc, now, granularity, windowMinutes);
    }

    /// <summary>
    /// Builds the full chart response: clone cached closed base, overlay live counters when needed,
    /// then merge and shape buckets per target and client.
    /// </summary>
    private async Task<TimeseriesSearchResponse> BuildResponseAsync(
        TimeseriesSearchRequest request,
        CancellationToken cancellationToken)
    {
        var closedBase = await GetOrCreateClosedBaseAsync(request, cancellationToken);
        var totalsByTargetClient = CloneTotals(closedBase.TotalsByTargetClient);

        if (IncludesNow(request.ToUtc))
        {
            await OverlayLiveCountersBatchedAsync(
                totalsByTargetClient,
                closedBase.TargetType,
                closedBase.TargetIds,
                closedBase.ClientIds,
                request.FromUtc,
                request.ToUtc,
                closedBase.Granularity,
                cancellationToken);
        }

        var capValues = await LoadCapValuesAsync(request.SearchCategory, closedBase.TargetIds, cancellationToken);
        var clientNames = await LoadClientNamesAsync(closedBase.ClientIds, cancellationToken);
        var targetNames = await LoadTargetNamesAsync(request.SearchCategory, closedBase.TargetIds, cancellationToken);
        var targets = new List<TimeseriesTargetSeries>();

        foreach (var targetId in closedBase.TargetIds)
        {
            var clientSeries = new List<TimeseriesClientSeries>();
            var aggregateSource = new SortedDictionary<DateTime, BucketTotals>();

            foreach (var clientId in closedBase.ClientIds)
            {
                if (!totalsByTargetClient.TryGetValue((targetId, clientId), out var clientBuckets) || clientBuckets.Count == 0)
                {
                    continue;
                }

                foreach (var (timestamp, totals) in clientBuckets)
                {
                    if (!aggregateSource.TryGetValue(timestamp, out var existing))
                    {
                        existing = new BucketTotals(0, 0, 0, 0, 0, 0, 0);
                    }

                    aggregateSource[timestamp] = existing.Add(totals);
                }

                var merged = StatisticsBucketMerger.Merge(
                    clientBuckets.Select(kvp => (kvp.Key, kvp.Value)),
                    request.FromUtc,
                    request.ToUtc,
                    request.BucketCount,
                    closedBase.Granularity,
                    closedBase.UseLatestForActive);

                if (merged.Count == 0)
                {
                    continue;
                }

                clientSeries.Add(new TimeseriesClientSeries(
                    clientId,
                    clientNames.GetValueOrDefault(clientId, clientId),
                    merged.Select(bucket => ToDisplayBucket(bucket)).ToList()));
            }

            var aggregateMerged = StatisticsBucketMerger.Merge(
                aggregateSource.Select(kvp => (kvp.Key, kvp.Value)),
                request.FromUtc,
                request.ToUtc,
                request.BucketCount,
                closedBase.Granularity,
                closedBase.UseLatestForActive);

            if (clientSeries.Count == 0 && aggregateMerged.Count == 0)
            {
                continue;
            }

            targets.Add(new TimeseriesTargetSeries(
                targetId,
                targetNames.GetValueOrDefault(targetId, targetId),
                capValues.GetValueOrDefault(targetId),
                aggregateMerged.Select(bucket => ToDisplayBucket(bucket)).ToList(),
                clientSeries));
        }

        return new TimeseriesSearchResponse(
            request.SearchCategory,
            closedBase.TargetType,
            closedBase.Granularity,
            targets);
    }

    /// <summary>
    /// Loads or creates the cached closed-base aggregate (snapshots only, no live overlay).
    /// </summary>
    /// <remarks>
    /// When the request includes the live tail, <c>snapshotToUtc</c> is pinned to <see cref="DateTime.UtcNow"/>
    /// inside the factory so the cached base always reflects rolled-up history through "now" at cache-fill time.
    /// Subsequent overlay work reconciles sub-second pending counters on every request.
    /// </remarks>
    private async Task<TimeseriesClosedBase> GetOrCreateClosedBaseAsync(
        TimeseriesSearchRequest request,
        CancellationToken cancellationToken)
    {
        var (targetType, useLatestForActive) = MapCategory(request.SearchCategory);
        var targetIds = await ResolveTargetIdsAsync(request.SearchCategory, request.TargetIds, cancellationToken);
        var clientIds = await ResolveClientIdsAsync(request.ClientIds, targetIds, targetType, cancellationToken);
        var granularity = StatisticsGranularityPicker.PickForRange(request.FromUtc, request.ToUtc, request.BucketCount);
        var cacheKey = CreateClosedCacheKey(request, request.BucketCount, granularity);

        return await _cache.GetOrCreateStatisticsClosedAsync(
            cacheKey,
            async token =>
            {
                var snapshotToUtc = IncludesNow(request.ToUtc) ? DateTime.UtcNow : request.ToUtc;
                var snapshots = targetIds.Count == 0 || clientIds.Count == 0
                    ? []
                    : await _usageSnapshotDatabase.GetByTargetsAndRangeAsync(
                        targetIds,
                        targetType,
                        granularity,
                        request.FromUtc,
                        snapshotToUtc,
                        clientIds,
                        token);

                return new TimeseriesClosedBase(
                    request.SearchCategory,
                    targetType,
                    useLatestForActive,
                    granularity,
                    targetIds,
                    clientIds,
                    AggregateSnapshots(snapshots, request.FromUtc, snapshotToUtc, granularity));
            },
            cancellationToken);
    }

    /// <summary>
    /// Builds the statistics-closed cache key for a timeseries query.
    /// </summary>
    /// <remarks>
    /// Live queries (those that include "now") omit <paramref name="request"/>.<c>ToUtc</c> from the key
    /// so dashboard polls with a sliding wall-clock end time hit the same cache entry. Historical-only
    /// queries include <c>toUtc</c> because the closed range is fixed.
    /// </remarks>
    private static string CreateClosedCacheKey(
        TimeseriesSearchRequest request,
        int bucketCount,
        BucketGranularity granularity)
    {
        var baseKey =
            $"timeseries:closed:{request.SearchCategory}:{CreateIdsKey(request.TargetIds)}:{CreateIdsKey(request.ClientIds)}:{request.FromUtc:O}:{bucketCount}:{granularity}";

        return IncludesNow(request.ToUtc) ? baseKey : $"{baseKey}:{request.ToUtc:O}";
    }

    /// <summary>
    /// Returns whether <paramref name="toUtc"/> is close enough to UTC now to require a live overlay.
    /// </summary>
    private static bool IncludesNow(DateTime toUtc) => toUtc >= DateTime.UtcNow.AddSeconds(-2);

    /// <summary>
    /// Cached snapshot aggregate and resolved query dimensions before live overlay and display merge.
    /// </summary>
    /// <param name="SearchCategory">Original search category from the request.</param>
    /// <param name="TargetType">Resolved service or resource-pool target type.</param>
    /// <param name="UseLatestForActive">Whether the active-count series uses the latest bucket value (pool allocations).</param>
    /// <param name="Granularity">Storage tier selected for this range and bucket count.</param>
    /// <param name="TargetIds">Resolved target identifiers (explicit or catalog-expanded).</param>
    /// <param name="ClientIds">Resolved client identifiers (explicit or catalog-expanded).</param>
    /// <param name="TotalsByTargetClient">Per (target, client) bucket totals from snapshots only.</param>
    private sealed record TimeseriesClosedBase(
        StatisticsSearchCategory SearchCategory,
        TargetType TargetType,
        bool UseLatestForActive,
        BucketGranularity Granularity,
        IReadOnlyList<string> TargetIds,
        IReadOnlyList<string> ClientIds,
        Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>> TotalsByTargetClient);

    /// <summary>
    /// Deep-copies closed-base totals so overlay mutations do not corrupt the cached entry.
    /// </summary>
    private static Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>> CloneTotals(
        Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>> source)
    {
        var clone = new Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>>(source.Count);
        foreach (var (key, buckets) in source)
        {
            clone[key] = new SortedDictionary<DateTime, BucketTotals>(buckets);
        }

        return clone;
    }

    private static (TargetType TargetType, bool UseLatestForActive) MapCategory(StatisticsSearchCategory category) =>
        category switch
        {
            StatisticsSearchCategory.ServiceRequests => (TargetType.Service, false),
            StatisticsSearchCategory.ResourcePoolAllocations => (TargetType.ResourcePool, true),
            StatisticsSearchCategory.ResourcePoolRequests => (TargetType.ResourcePool, false),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };

    private async Task<IReadOnlyList<string>> ResolveTargetIdsAsync(
        StatisticsSearchCategory category,
        IReadOnlyList<string>? targetIds,
        CancellationToken cancellationToken)
    {
        if (targetIds is { Count: > 0 })
        {
            return NormalizeIds(targetIds);
        }

        return category switch
        {
            StatisticsSearchCategory.ServiceRequests => NormalizeIds(
                (await GetCachedServicesAsync(cancellationToken)).Select(service => service.Id)),
            _ => NormalizeIds(
                (await GetCachedPoolsAsync(cancellationToken)).Select(pool => pool.Id))
        };
    }

    private async Task<IReadOnlyList<string>> ResolveClientIdsAsync(
        IReadOnlyList<string>? clientIds,
        IReadOnlyList<string> targetIds,
        TargetType targetType,
        CancellationToken cancellationToken)
    {
        if (clientIds is { Count: > 0 })
        {
            return NormalizeIds(clientIds);
        }

        if (targetIds.Count == 0)
        {
            return [];
        }

        var clients = await GetCachedClientsAsync(cancellationToken);
        var targetIdSet = targetIds.ToHashSet(StringComparer.Ordinal);
        var resolved = new List<string>();

        foreach (var client in clients)
        {
            var hasTarget = targetType == TargetType.Service
                ? client.Services.Keys.Any(targetIdSet.Contains)
                : client.ResourcePools.Keys.Any(targetIdSet.Contains);

            if (hasTarget)
            {
                resolved.Add(client.Id);
            }
        }

        return NormalizeIds(resolved);
    }

    private Task<IReadOnlyList<ClientConfiguration>> GetCachedClientsAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync(
            "clients:all",
            _clientConfigDatabase.GetAllAsync,
            cancellationToken);

    private Task<IReadOnlyList<Service>> GetCachedServicesAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync(
            "services:all",
            async token =>
            {
                var result = await _serviceRepository.SearchAsync(DocumentQuery.All, token);
                return (IReadOnlyList<Service>)result.Items;
            },
            cancellationToken);

    private Task<IReadOnlyList<ResourcePool>> GetCachedPoolsAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateCatalogAsync(
            "pools:all",
            async token =>
            {
                var result = await _poolRepository.SearchAsync(DocumentQuery.All, token);
                return (IReadOnlyList<ResourcePool>)result.Items;
            },
            cancellationToken);

    private async Task<IReadOnlyDictionary<string, double>> LoadCapValuesAsync(
        StatisticsSearchCategory category,
        IReadOnlyList<string> targetIds,
        CancellationToken cancellationToken)
    {
        if (targetIds.Count == 0)
        {
            return new Dictionary<string, double>(StringComparer.Ordinal);
        }

        var targetIdSet = targetIds.ToHashSet(StringComparer.Ordinal);

        if (category == StatisticsSearchCategory.ResourcePoolAllocations)
        {
            var caps = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var targetId in targetIds)
            {
                var pool = await _poolRepository.GetByIdAsync(targetId, cancellationToken);
                if (pool is not null)
                {
                    caps[targetId] = pool.MaxSlots;
                }
            }

            return caps;
        }

        var targetType = category == StatisticsSearchCategory.ServiceRequests
            ? TargetType.Service
            : TargetType.ResourcePool;
        var limits = await _globalRateLimitDatabase.GetByTargetTypeAsync(targetType, cancellationToken);
        return limits
            .Where(limit => targetIdSet.Contains(limit.TargetId))
            .GroupBy(limit => limit.TargetId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (double)group.First().MaxRequests, StringComparer.Ordinal);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadClientNamesAsync(
        IReadOnlyList<string> clientIds,
        CancellationToken cancellationToken)
    {
        if (clientIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var clientId in clientIds)
        {
            var client = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken);
            names[clientId] = client?.Name ?? clientId;
        }

        return names;
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadTargetNamesAsync(
        StatisticsSearchCategory category,
        IReadOnlyList<string> targetIds,
        CancellationToken cancellationToken)
    {
        if (targetIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (category == StatisticsSearchCategory.ServiceRequests)
        {
            foreach (var targetId in targetIds)
            {
                var service = await _serviceRepository.GetByIdAsync(targetId, cancellationToken);
                names[targetId] = service?.Name ?? targetId;
            }

            return names;
        }

        foreach (var targetId in targetIds)
        {
            var pool = await _poolRepository.GetByIdAsync(targetId, cancellationToken);
            names[targetId] = pool?.Name ?? targetId;
        }

        return names;
    }

    private static Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>> AggregateSnapshots(
        IReadOnlyList<UsageSnapshot> snapshots,
        DateTime fromUtc,
        DateTime toUtc,
        BucketGranularity granularity)
    {
        var result = new Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>>();
        var storageDuration = BucketGranularityHelper.GetBucketDuration(granularity);

        foreach (var snapshot in snapshots)
        {
            var key = (snapshot.TargetId, snapshot.ClientId);
            if (!result.TryGetValue(key, out var buckets))
            {
                buckets = [];
                result[key] = buckets;
            }

            foreach (var bucket in snapshot.Buckets)
            {
                if (!BucketGranularityHelper.OverlapsRange(bucket.Timestamp, storageDuration, fromUtc, toUtc))
                {
                    continue;
                }

                var timestamp = BucketGranularityHelper.RoundDown(granularity, bucket.Timestamp);
                if (!buckets.TryGetValue(timestamp, out var totals))
                {
                    totals = new BucketTotals(0, 0, 0, 0, 0, 0, 0);
                }

                buckets[timestamp] = totals.Add(BucketTotals.FromUsageBucket(bucket));
            }
        }

        return result;
    }

    /// <summary>
    /// Applies pending second-level usage counters onto closed-base totals in one storage round-trip.
    /// </summary>
    /// <remarks>
    /// Replaces the prior per-(target, client) prefix scan with a single <c>usage:</c> prefix read
    /// (deduped in <c>UsageSnapshotDatabase</c>) and in-memory distribution. Hour/day granularities
    /// skip overlay because second-level counters cannot affect those display buckets.
    /// </remarks>
    private async Task OverlayLiveCountersBatchedAsync(
        Dictionary<(string TargetId, string ClientId), SortedDictionary<DateTime, BucketTotals>> totalsByTargetClient,
        TargetType targetType,
        IReadOnlyList<string> targetIds,
        IReadOnlyList<string> clientIds,
        DateTime fromUtc,
        DateTime toUtc,
        BucketGranularity granularity,
        CancellationToken cancellationToken)
    {
        var overlayFrom = MaxDateTime(fromUtc, DateTime.UtcNow - _usageTrackingOptions.SecondRetention);
        if (overlayFrom > toUtc || granularity is BucketGranularity.Hour or BucketGranularity.Day)
        {
            return;
        }

        var targetIdSet = targetIds.ToHashSet(StringComparer.Ordinal);
        var clientIdSet = clientIds.ToHashSet(StringComparer.Ordinal);
        var overlayStart = UsageSegmentHelper.RoundDownToSecond(overlayFrom);
        var overlayEnd = UsageSegmentHelper.RoundDownToSecond(toUtc);

        var counters = await _usageSnapshotDatabase.GetPendingCounterValuesByPrefixAsync("usage:", cancellationToken);
        foreach (var (storageKey, value) in counters)
        {
            if (value <= 0 ||
                !UsageSegmentHelper.TryParseUsageCounterKey(
                    storageKey,
                    out var clientId,
                    out var parsedTargetType,
                    out var targetId,
                    out var secondTimestamp,
                    out var eventType,
                    out var denialCategory))
            {
                continue;
            }

            if (parsedTargetType != targetType ||
                !targetIdSet.Contains(targetId) ||
                !clientIdSet.Contains(clientId) ||
                secondTimestamp < overlayStart ||
                secondTimestamp > overlayEnd)
            {
                continue;
            }

            var key = (targetId, clientId);
            if (!totalsByTargetClient.TryGetValue(key, out var buckets))
            {
                buckets = [];
                totalsByTargetClient[key] = buckets;
            }

            var bucketTimestamp = BucketGranularityHelper.RoundDown(granularity, secondTimestamp);
            if (!buckets.TryGetValue(bucketTimestamp, out var totals))
            {
                totals = new BucketTotals(0, 0, 0, 0, 0, 0, 0);
            }

            buckets[bucketTimestamp] = ApplyCounterEvent(totals, eventType, denialCategory, value);
        }
    }

    private static BucketTotals ApplyCounterEvent(
        BucketTotals totals,
        UsageEventType eventType,
        UsageDenialCategory? denialCategory,
        long value) => eventType switch
    {
        UsageEventType.Granted => totals with
        {
            Granted = totals.Granted + value,
            Active = totals.Active + value
        },
        UsageEventType.Denied => denialCategory switch
        {
            UsageDenialCategory.Unauthenticated => totals with { DeniedUnauthenticated = totals.DeniedUnauthenticated + value },
            UsageDenialCategory.Blocked => totals with { DeniedBlocked = totals.DeniedBlocked + value },
            UsageDenialCategory.RateLimited => totals with { DeniedRateLimited = totals.DeniedRateLimited + value },
            UsageDenialCategory.CapacityLimited => totals with { DeniedCapacityLimited = totals.DeniedCapacityLimited + value },
            _ => totals with { DeniedBlocked = totals.DeniedBlocked + value }
        },
        UsageEventType.Released => totals with
        {
            Released = totals.Released + value,
            Active = Math.Max(0, totals.Active - value)
        },
        _ => totals
    };

    private static TimeseriesDisplayBucket ToDisplayBucket(
        (string Label, DateTime Start, DateTime End, BucketTotals Totals) bucket) =>
        new(
            bucket.Label,
            bucket.Start,
            bucket.End,
            bucket.Totals.Granted,
            bucket.Totals.DeniedUnauthenticated,
            bucket.Totals.DeniedBlocked,
            bucket.Totals.DeniedRateLimited,
            bucket.Totals.DeniedCapacityLimited,
            bucket.Totals.Released,
            bucket.Totals.Active);

    private static IReadOnlyList<string> NormalizeIds(IEnumerable<string> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

    private static string CreateIdsKey(IReadOnlyList<string>? ids) =>
        ids is null or { Count: 0 } ? "*" : string.Join(',', NormalizeIds(ids));

    private static DateTime MaxDateTime(DateTime left, DateTime right) => left > right ? left : right;
}
