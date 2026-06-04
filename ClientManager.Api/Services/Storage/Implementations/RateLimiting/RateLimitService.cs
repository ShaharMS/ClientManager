using System.Diagnostics;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.Api.Services.Storage.Models.Entities;
using ClientManager.Api.Services.Storage.Models.Enums;
using ClientManager.Api.Services.Storage.Interfaces;
using ClientManager.Api.Services.Storage.Utils.Extensions;
using ClientManager.Api.Services.Storage.Utils.Instrumentation;

namespace ClientManager.Api.Services.Storage.Implementations.RateLimiting;

/// <summary>
/// Evaluates client and global rate-limit state inside the storage boundary.
/// </summary>
public class RateLimitService : IRateLimitService
{
    private const double SlowRateLimitThresholdMs = 250;

    private readonly IAppLogger<RateLimitService> _logger;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly RateLimitStrategyResolver _strategyResolver;
    private readonly StorageApiMetrics _metrics;
    private readonly IStorageReadCache _cache;

    public RateLimitService(
        IAppLogger<RateLimitService> logger,
        IClientConfigurationDatabase clientConfigDatabase,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        RateLimitStrategyResolver strategyResolver,
        StorageApiMetrics metrics,
        IStorageReadCache cache)
    {
        _logger = logger;
        _clientConfigDatabase = clientConfigDatabase;
        _globalRateLimitDatabase = globalRateLimitDatabase;
        _strategyResolver = strategyResolver;
        _metrics = metrics;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckAndIncrementAsync(
        ClientConfiguration configuration,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        return await TraceRateLimitAsync(
            "storage.ratelimit.client_check",
            "client_check",
            activity =>
            {
                activity?.SetTag("client.id", configuration.Id);
                activity?.SetTag("service.id", serviceId);
                activity?.SetTag("ratelimit.mode", "increment");
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
                var result = Combine(serviceResult, globalResult);
                return CompleteClientDecision(result, configuration.Id, serviceId);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckGlobalServiceLimitAsync(
        ClientConfiguration configuration,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var settings = configuration.Services.GetValueOrDefault(serviceId);

        return await CheckGlobalLimitAsync(
            configuration,
            serviceId,
            TargetType.Service,
            settings?.ContributesToGlobalLimit ?? configuration.ContributesToGlobalLimits,
            settings?.ExemptFromGlobalLimit ?? configuration.ExemptFromGlobalLimits,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(
        ClientConfiguration configuration,
        string resourcePoolId,
        CancellationToken cancellationToken = default)
    {
        return await CheckGlobalLimitAsync(
            configuration,
            resourcePoolId,
            TargetType.ResourcePool,
            configuration.ContributesToGlobalLimits,
            configuration.ExemptFromGlobalLimits,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckWithoutIncrementAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        return await TraceRateLimitAsync(
            "storage.ratelimit.client_peek",
            "client_peek",
            activity =>
            {
                activity?.SetTag("client.id", clientId);
                activity?.SetTag("service.id", serviceId);
                activity?.SetTag("ratelimit.mode", "peek");
            },
            async () =>
            {
                var configuration = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken);
                if (configuration is null || !configuration.IsEnabled)
                {
                    return Allowed();
                }

                var serviceResult = await EvaluateAsync(
                    configuration.Services.GetValueOrDefault(serviceId)?.RateLimit,
                    $"{clientId}:{serviceId}",
                    increment: false,
                    cancellationToken);

                var globalResult = await EvaluateAsync(
                    configuration.GlobalRateLimit,
                    $"{clientId}:global",
                    increment: false,
                    cancellationToken);

                return Combine(serviceResult, globalResult);
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
            activity?.SetTag("ratelimit.result", "canceled");
            activity?.SetTag("duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            _logger.Debug("Rate limit operation canceled", new
            {
                Operation = operation,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                Result = "canceled"
            });
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            activity?.SetTag("error.type", exception.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.Error("Rate limit operation failed", new
            {
                Operation = operation,
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                Result = "exception"
            }, exception);
            throw;
        }
    }

    private async Task<RateLimitResult> CheckGlobalLimitAsync(
        ClientConfiguration configuration,
        string targetId,
        TargetType targetType,
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
                activity?.SetTag("ratelimit.target_id", targetId);
                activity?.SetTag("ratelimit.target_type", targetType.ToString());
                activity?.SetTag("ratelimit.contributes_to_global", contributesToGlobal);
                activity?.SetTag("ratelimit.exempt_from_global", exemptFromGlobal);
            },
            async () =>
            {
                var globalLimit = await GetCachedGlobalLimitAsync(targetId, targetType, cancellationToken);
                if (globalLimit is null)
                {
                    return Allowed();
                }

                var strategy = _strategyResolver.Resolve(globalLimit.Strategy);
                var globalKey = targetType == TargetType.Service
                    ? $"global:service:{targetId}"
                    : $"global:pool:{targetId}";
                var rateLimit = ToClientRateLimit(globalLimit);

                if (contributesToGlobal)
                {
                    var result = await strategy.EvaluateAsync(globalKey, rateLimit, cancellationToken);
                    return exemptFromGlobal ? Allowed() : CompleteGlobalDecision(result, configuration.Id, targetId, targetType);
                }

                if (exemptFromGlobal)
                {
                    return Allowed();
                }

                var peekResult = await strategy.PeekAsync(globalKey, rateLimit, cancellationToken);
                return CompleteGlobalDecision(peekResult, configuration.Id, targetId, targetType);
            },
            cancellationToken);
    }

    private RateLimitResult CompleteClientDecision(
        RateLimitResult result,
        string clientId,
        string serviceId)
    {
        RecordClientRateLimitDecision(result, clientId, serviceId);

        _logger.Debug("Rate limit evaluated", new
        {
            ClientId = clientId,
            ServiceId = serviceId,
            result.IsAllowed,
            result.RemainingRequests
        });

        return result;
    }

    private RateLimitResult CompleteGlobalDecision(
        RateLimitResult result,
        string clientId,
        string targetId,
        TargetType targetType)
    {
        if (result.IsAllowed)
        {
            return result;
        }

        RecordGlobalHit(clientId, targetId, targetType);
        return result with { IsGlobalLimitHit = true };
    }

    private async Task<RateLimitResult?> EvaluateAsync(
        ClientRateLimit? rateLimit,
        string key,
        bool increment,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.ratelimit.strategy_dispatch",
            act =>
            {
                act?.SetTag("ratelimit.configured", rateLimit is not null);
                act?.SetTag("ratelimit.mode", increment ? "increment" : "peek");
            });

        if (rateLimit is null)
        {
            activity?.SetTag("ratelimit.result", "not_configured");
            return null;
        }

        activity?.SetTag("ratelimit.strategy", rateLimit.Strategy.ToString());
        var strategy = _strategyResolver.Resolve(rateLimit.Strategy);
        var result = increment
            ? await strategy.EvaluateAsync(key, rateLimit, cancellationToken)
            : await strategy.PeekAsync(key, rateLimit, cancellationToken);

        activity?.SetTag("ratelimit.result", result.IsAllowed ? "allowed" : "denied");
        return result;
    }

    private async Task<GlobalRateLimit?> GetCachedGlobalLimitAsync(
        string targetId,
        TargetType targetType,
        CancellationToken cancellationToken)
    {
        using var activity = _metrics.ActivitySource.StartInternalActivity(
            "storage.ratelimit.global_limit_read",
            act =>
            {
                act?.SetTag("ratelimit.target_id", targetId);
                act?.SetTag("ratelimit.target_type", targetType.ToString());
            });

        var globalLimit = await _cache.GetOrCreateCatalogAsync(
            $"global-limit:{targetId}:{targetType}",
            token => _globalRateLimitDatabase.GetByTargetAsync(targetId, targetType, token),
            cancellationToken);
        activity?.SetTag("ratelimit.global_limit_found", globalLimit is not null);
        return globalLimit;
    }

    private void CompleteRateLimit(
        Activity? activity,
        string operation,
        RateLimitResult result,
        double durationMs)
    {
        var outcome = result.IsAllowed ? "allowed" : "denied";
        activity?.SetTag("ratelimit.result", outcome);
        activity?.SetTag("ratelimit.remaining_requests", result.RemainingRequests);
        activity?.SetTag("ratelimit.retry_after_seconds", result.RetryAfterSeconds);
        activity?.SetTag("duration_ms", durationMs);

        LogRateLimitCompletion(operation, outcome, result, durationMs);
    }

    private void LogRateLimitCompletion(
        string operation,
        string outcome,
        RateLimitResult result,
        double durationMs)
    {
        var extraData = new
        {
            Operation = operation,
            DurationMs = durationMs,
            Result = outcome,
            result.RemainingRequests,
            result.RetryAfterSeconds
        };

        if (durationMs >= SlowRateLimitThresholdMs)
        {
            _logger.Warn("Rate limit operation completed slowly", extraData);
            return;
        }

        if (outcome == "denied")
        {
            _logger.Info("Rate limit operation denied", extraData);
            return;
        }

        _logger.Debug("Rate limit operation completed", extraData);
    }

    private void RecordClientRateLimitDecision(
        RateLimitResult result,
        string clientId,
        string serviceId)
    {
        var counter = result.IsAllowed ? _metrics.RateLimitAllowed : _metrics.RateLimitDenied;
        counter.Add(1, new TagList
        {
            { MetricTagKey.ClientId.ToTagName(), clientId },
            { MetricTagKey.ServiceId.ToTagName(), serviceId }
        });
    }

    private void RecordGlobalHit(string clientId, string targetId, TargetType targetType)
    {
        var tags = new TagList { { MetricTagKey.ClientId.ToTagName(), clientId } };

        if (targetType == TargetType.Service)
        {
            tags.Add(MetricTagKey.ServiceId.ToTagName(), targetId);
        }
        else
        {
            tags.Add(MetricTagKey.ResourcePoolId.ToTagName(), targetId);
        }

        _metrics.GlobalRateLimitHits.Add(1, tags);
    }

    private static RateLimitResult Allowed() => new()
    {
        IsAllowed = true,
        RemainingRequests = int.MaxValue
    };

    private static RateLimitResult Combine(RateLimitResult? first, RateLimitResult? second)
    {
        if (first is null && second is null)
        {
            return Allowed();
        }

        if (first is null)
        {
            return second!;
        }

        if (second is null)
        {
            return first;
        }

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

    private static ClientRateLimit ToClientRateLimit(GlobalRateLimit limit) => new()
    {
        Strategy = limit.Strategy,
        MaxRequests = limit.MaxRequests,
        Window = limit.Window,
        TokensPerRefill = limit.TokensPerRefill
    };
}