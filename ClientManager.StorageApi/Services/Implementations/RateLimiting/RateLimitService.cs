using System.Diagnostics;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using ClientManager.StorageApi.Models.Entities;
using ClientManager.StorageApi.Models.Enums;
using ClientManager.StorageApi.Services.Interfaces;
using ClientManager.StorageApi.Utils.Extensions;
using ClientManager.StorageApi.Utils.Instrumentation;

namespace ClientManager.StorageApi.Services.Implementations.RateLimiting;

/// <summary>
/// Evaluates client and global rate-limit state inside the storage boundary.
/// </summary>
public class RateLimitService : IRateLimitService
{
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
        if (!configuration.IsEnabled)
        {
            return Allowed();
        }

        var serviceResult = await EvaluateAsync(
            configuration.Services.GetValueOrDefault(serviceId)?.RateLimit,
            $"{configuration.Id}:{serviceId}",
            increment: true,
            cancellationToken);

        var globalResult = await EvaluateAsync(
            configuration.GlobalRateLimit,
            $"{configuration.Id}:global",
            increment: true,
            cancellationToken);

        var result = Combine(serviceResult, globalResult);
        RecordClientRateLimitDecision(result, configuration.Id, serviceId);

        _logger.Debug("Rate limit evaluated", new
        {
            ClientId = configuration.Id,
            ServiceId = serviceId,
            result.IsAllowed,
            result.RemainingRequests
        });

        return result;
    }

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
            TargetType.Service,
            settings?.ContributesToGlobalLimit ?? configuration.ContributesToGlobalLimits,
            settings?.ExemptFromGlobalLimit ?? configuration.ExemptFromGlobalLimits,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(
        ClientConfiguration configuration,
        string resourcePoolId,
        CancellationToken cancellationToken = default)
    {
        return CheckGlobalLimitAsync(
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
    }

    private async Task<RateLimitResult> CheckGlobalLimitAsync(
        ClientConfiguration configuration,
        string targetId,
        TargetType targetType,
        bool contributesToGlobal,
        bool exemptFromGlobal,
        CancellationToken cancellationToken)
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

        if (contributesToGlobal)
        {
            await strategy.EvaluateAsync(globalKey, ToClientRateLimit(globalLimit), cancellationToken);
        }

        if (exemptFromGlobal)
        {
            return Allowed();
        }

        var result = await strategy.PeekAsync(globalKey, ToClientRateLimit(globalLimit), cancellationToken);
        if (result.IsAllowed)
        {
            return result;
        }

        RecordGlobalHit(configuration.Id, targetId, targetType);
        return result with { IsGlobalLimitHit = true };
    }

    private async Task<RateLimitResult?> EvaluateAsync(
        ClientRateLimit? rateLimit,
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

    private async Task<GlobalRateLimit?> GetCachedGlobalLimitAsync(
        string targetId,
        TargetType targetType,
        CancellationToken cancellationToken) =>
        await _cache.GetOrCreateCatalogAsync(
            $"global-limit:{targetId}:{targetType}",
            token => _globalRateLimitDatabase.GetByTargetAsync(targetId, targetType, token),
            cancellationToken);

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