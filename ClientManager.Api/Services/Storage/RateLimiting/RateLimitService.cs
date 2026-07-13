using System.Diagnostics;
using ClientManager.Api.Storage.Databases.Interfaces;
using ClientManager.Shared.Configuration.Storage;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Services.Storage.Extensions;
using ClientManager.Api.Services.Storage.Instrumentation;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage.RateLimiting;

/// <summary>
/// Evaluates client and global rate-limit state inside the storage boundary.
/// </summary>
public class RateLimitService : IRateLimitService
{
    private const double SlowRateLimitThresholdMs = 250;

    private readonly IAppLogger<RateLimitService> _logger;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly RateLimitStrategyResolver _strategyResolver;
    private readonly StorageMetrics _metrics;
    private readonly IStorageReadCache _cache;
    private readonly StorageReadCacheOptions _cacheOptions;

    public RateLimitService(
        IAppLogger<RateLimitService> logger,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        RateLimitStrategyResolver strategyResolver,
        StorageMetrics metrics,
        IStorageReadCache cache,
        IOptions<StorageReadCacheOptions> cacheOptions)
    {
        _logger = logger;
        _globalRateLimitDatabase = globalRateLimitDatabase;
        _strategyResolver = strategyResolver;
        _metrics = metrics;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    /// <inheritdoc />
    public Task<RateLimitResult> CheckAndIncrementAsync(
        ClientConfiguration configuration,
        string serviceId,
        CancellationToken cancellationToken = default) =>
        TraceRateLimitAsync(
            "storage.ratelimit.client_check",
            "client_check",
            activity =>
            {
                activity?.SetTag("client.id", configuration.Id);
                activity?.SetTag("service.id", serviceId);
            },
            async () =>
            {
                if (!configuration.IsEnabled)
                {
                    return Allowed();
                }

                var serviceResult = await EvaluateAsync(
                    configuration.Services.GetValueOrDefault(serviceId)?.RateLimit,
                    $"{configuration.Id}:{serviceId}",
                    increment: true,
                    cancellationToken);

                if (serviceResult is { IsAllowed: false })
                {
                    return CompleteClientDecision(serviceResult, configuration.Id, serviceId);
                }

                var globalResult = await EvaluateAsync(
                    configuration.GlobalRateLimit,
                    $"{configuration.Id}:global",
                    increment: true,
                    cancellationToken);

                return CompleteClientDecision(Combine(serviceResult, globalResult), configuration.Id, serviceId);
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<RateLimitResult> CheckGlobalServiceLimitAsync(
        ClientConfiguration configuration,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var settings = configuration.Services.GetValueOrDefault(serviceId);
        return CheckGlobalLimitAsync(
            configuration,
            serviceId,
            settings?.ContributesToGlobalLimit ?? configuration.ContributesToGlobalLimits,
            settings?.ExemptFromGlobalLimit ?? configuration.ExemptFromGlobalLimits,
            cancellationToken);
    }

    private async Task<RateLimitResult> CheckGlobalLimitAsync(
        ClientConfiguration configuration,
        string serviceId,
        bool contributesToGlobal,
        bool exemptFromGlobal,
        CancellationToken cancellationToken)
    {
        return await TraceRateLimitAsync(
            "storage.ratelimit.global_check",
            "global_check",
            activity =>
            {
                activity?.SetTag("client.id", configuration.Id);
                activity?.SetTag("service.id", serviceId);
            },
            async () =>
            {
                var globalLimit = await GetGlobalLimitAsync(serviceId, cancellationToken);
                if (globalLimit is null)
                {
                    return Allowed();
                }

                var strategy = _strategyResolver.Resolve(globalLimit.Policy.Strategy);
                var globalKey = $"global:service:{serviceId}";

                if (contributesToGlobal)
                {
                    var result = await strategy.EvaluateAsync(globalKey, globalLimit.Policy, cancellationToken);
                    return exemptFromGlobal ? Allowed() : CompleteGlobalDecision(result, configuration.Id, serviceId);
                }

                if (exemptFromGlobal)
                {
                    return Allowed();
                }

                var peekResult = await strategy.PeekAsync(globalKey, globalLimit.Policy, cancellationToken);
                return CompleteGlobalDecision(peekResult, configuration.Id, serviceId);
            },
            cancellationToken);
    }

    private async Task<RateLimitResult> TraceRateLimitAsync(
        string activityName,
        string operation,
        Action<Activity?> configureActivity,
        Func<Task<RateLimitResult>> action,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(activityName, configureActivity);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await action();
            stopwatch.Stop();
            CompleteRateLimit(activity, operation, result, stopwatch.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            activity?.SetTag("error.type", exception.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.Error("Rate limit operation failed", new { Operation = operation }, exception);
            throw;
        }
    }

    private RateLimitResult CompleteClientDecision(RateLimitResult result, string clientId, string serviceId)
    {
        var counter = result.IsAllowed ? _metrics.RateLimitAllowed : _metrics.RateLimitDenied;
        counter.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId }
        });
        return result;
    }

    private static RateLimitResult CompleteGlobalDecision(RateLimitResult result, string clientId, string serviceId) =>
        result.IsAllowed ? result : result with { IsGlobalLimitHit = true };

    private async Task<RateLimitResult?> EvaluateAsync(
        RateLimitPolicy? rateLimit,
        string key,
        bool increment,
        CancellationToken cancellationToken)
    {
        if (rateLimit is null)
        {
            return null;
        }

        var strategy = _strategyResolver.Resolve(rateLimit.Strategy);
        return increment
            ? await strategy.EvaluateAsync(key, rateLimit, cancellationToken)
            : await strategy.PeekAsync(key, rateLimit, cancellationToken);
    }

    private async Task<GlobalRateLimit?> GetGlobalLimitAsync(string serviceId, CancellationToken cancellationToken) =>
        await _cache.GetOrCreateCatalogAsync(
            $"global-limit:{serviceId}",
            token => _globalRateLimitDatabase.GetByServiceIdAsync(serviceId, token),
            cancellationToken,
            _cacheOptions.HotPathCatalogTtl);

    private static void CompleteRateLimit(Activity? activity, string operation, RateLimitResult result, double durationMs)
    {
        activity?.SetTag("ratelimit.result", result.IsAllowed ? "allowed" : "denied");
        activity?.SetTag("duration_ms", durationMs);
    }

    private static RateLimitResult Allowed() => new() { IsAllowed = true, RemainingRequests = int.MaxValue };

    private static RateLimitResult Combine(RateLimitResult? first, RateLimitResult? second)
    {
        if (first is null && second is null) return Allowed();
        if (first is null) return second!;
        if (second is null) return first;
        if (!first.IsAllowed || !second.IsAllowed)
        {
            if (!first.IsAllowed && !second.IsAllowed)
            {
                return (first.RetryAfterSeconds ?? 0) >= (second.RetryAfterSeconds ?? 0) ? first : second;
            }

            return !first.IsAllowed ? first : second;
        }

        return first.RemainingRequests <= second.RemainingRequests ? first : second;
    }
}
