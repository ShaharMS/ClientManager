using System.Diagnostics;

using ClientManager.Api.Models.Entities;
using ClientManager.Api.Models.Enums;
using ClientManager.Api.Services.Interfaces;
using ClientManager.Api.Utils.Extensions;
using ClientManager.Api.Utils.Instrumentation;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace ClientManager.Api.Services.Implementations.RateLimiting;

/// <summary>
/// Evaluates rate limit policies at per-client, per-service, and global scopes.
/// Reads rate limit configurations from <see cref="IClientConfigurationDatabase"/>
/// and global limits from <see cref="IGlobalRateLimitDatabase"/>. Delegates the
/// actual algorithm execution to the <see cref="IRateLimitStrategy"/> resolved for
/// each rate limit's configured strategy type. Caches global rate limit lookups
/// briefly to reduce database load under high traffic.
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly IAppLogger<RateLimitService> _logger;
    private readonly IClientConfigurationDatabase _clientConfigDatabase;
    private readonly IGlobalRateLimitDatabase _globalRateLimitDatabase;
    private readonly RateLimitStrategyResolver _strategyResolver;
    private readonly ClientManagerMetrics _metrics;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan GlobalLimitCacheTtl = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="clientConfigDatabase">Database for client configurations.</param>
    /// <param name="globalRateLimitDatabase">Database for global rate limits.</param>
    /// <param name="strategyResolver">Resolver for rate limit strategy implementations.</param>
    /// <param name="metrics">The metrics instrumentation instance.</param>
    /// <param name="cache">Memory cache for short-lived global rate limit lookups.</param>
    public RateLimitService(
        IAppLogger<RateLimitService> logger,
        IClientConfigurationDatabase clientConfigDatabase,
        IGlobalRateLimitDatabase globalRateLimitDatabase,
        RateLimitStrategyResolver strategyResolver,
        ClientManagerMetrics metrics,
        IMemoryCache cache)
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
        ClientConfiguration config,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        if (!config.IsEnabled)
        {
            return Allowed();
        }

        var clientId = config.Id;
        var serviceRateLimit = config.Services.GetValueOrDefault(serviceId)?.RateLimit;
        var globalRateLimit = config.GlobalRateLimit;

        RateLimitResult? serviceResult = null;
        RateLimitResult? globalResult = null;

        if (serviceRateLimit is not null)
        {
            var strategy = _strategyResolver.Resolve(serviceRateLimit.Strategy);
            serviceResult = await strategy.EvaluateAsync($"{clientId}:{serviceId}", serviceRateLimit, cancellationToken);
        }

        if (globalRateLimit is not null)
        {
            var strategy = _strategyResolver.Resolve(globalRateLimit.Strategy);
            globalResult = await strategy.EvaluateAsync($"{clientId}:global", globalRateLimit, cancellationToken);
        }

        var result = CombineResults(serviceResult, globalResult);

        if (result.IsAllowed)
            _metrics.RateLimitAllowed.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId }
            });
        else
            _metrics.RateLimitDenied.Add(1, new TagList
            {
                { MetricTagKey.ClientId.ToTagName(), clientId },
                { MetricTagKey.ServiceId.ToTagName(), serviceId }
            });

        _logger.Debug("Rate limit evaluated", new { ClientId = clientId, ServiceId = serviceId, Allowed = result.IsAllowed, Remaining = result.RemainingRequests });

        return result;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckGlobalAndIncrementAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken);
        if (config?.GlobalRateLimit is null)
        {
            return Allowed();
        }

        var strategy = _strategyResolver.Resolve(config.GlobalRateLimit.Strategy);
        return await strategy.EvaluateAsync($"{clientId}:global", config.GlobalRateLimit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckGlobalServiceLimitAsync(
        ClientConfiguration config,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var clientId = config.Id;
        var serviceSettings = config.Services.GetValueOrDefault(serviceId);
        var contributesToGlobal = serviceSettings?.ContributesToGlobalLimit ?? config.ContributesToGlobalLimits;
        var exemptFromGlobal = serviceSettings?.ExemptFromGlobalLimit ?? config.ExemptFromGlobalLimits;

        var globalLimit = await GetCachedGlobalLimitAsync(
            serviceId, TargetType.Service, cancellationToken);

        if (globalLimit is null)
        {
            return Allowed();
        }

        var globalRateLimit = ToClientRateLimit(globalLimit);
        var strategy = _strategyResolver.Resolve(globalLimit.Strategy);
        var globalKey = $"global:service:{serviceId}";

        if (contributesToGlobal)
        {
            await strategy.EvaluateAsync(globalKey, globalRateLimit, cancellationToken);
        }

        RateLimitResult result;
        if (exemptFromGlobal)
        {
            result = Allowed();
        }
        else
        {
            result = await strategy.PeekAsync(globalKey, globalRateLimit, cancellationToken);
            if (!result.IsAllowed)
            {
                result = result with { IsGlobalLimitHit = true };
                _metrics.GlobalRateLimitHits.Add(1, new TagList
                {
                    { MetricTagKey.ClientId.ToTagName(), clientId },
                    { MetricTagKey.ServiceId.ToTagName(), serviceId }
                });
            }
        }

        _logger.Info("Global service limit checked", new { ClientId = clientId, ServiceId = serviceId, ContributesToGlobal = contributesToGlobal, ExemptFromGlobal = exemptFromGlobal, Allowed = result.IsAllowed });

        return result;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(
        ClientConfiguration config,
        string resourcePoolId,
        CancellationToken cancellationToken = default)
    {
        var clientId = config.Id;
        var contributesToGlobal = config.ContributesToGlobalLimits;
        var exemptFromGlobal = config.ExemptFromGlobalLimits;

        var globalLimit = await GetCachedGlobalLimitAsync(
            resourcePoolId, TargetType.ResourcePool, cancellationToken);

        if (globalLimit is null)
        {
            return Allowed();
        }

        var globalRateLimit = ToClientRateLimit(globalLimit);
        var strategy = _strategyResolver.Resolve(globalLimit.Strategy);
        var globalKey = $"global:pool:{resourcePoolId}";

        if (contributesToGlobal)
        {
            await strategy.EvaluateAsync(globalKey, globalRateLimit, cancellationToken);
        }

        RateLimitResult result;
        if (exemptFromGlobal)
        {
            result = Allowed();
        }
        else
        {
            result = await strategy.PeekAsync(globalKey, globalRateLimit, cancellationToken);
            if (!result.IsAllowed)
            {
                result = result with { IsGlobalLimitHit = true };
                _metrics.GlobalRateLimitHits.Add(1, new TagList
                {
                    { MetricTagKey.ClientId.ToTagName(), clientId },
                    { MetricTagKey.ResourcePoolId.ToTagName(), resourcePoolId }
                });
            }
        }

        _logger.Info("Global resource pool limit checked", new { ClientId = clientId, ResourcePoolId = resourcePoolId, ContributesToGlobal = contributesToGlobal, ExemptFromGlobal = exemptFromGlobal, Allowed = result.IsAllowed });

        return result;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckWithoutIncrementAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigDatabase.GetByIdAsync(clientId, cancellationToken);
        if (config is null || !config.IsEnabled)
        {
            return Allowed();
        }

        var serviceRateLimit = config.Services.GetValueOrDefault(serviceId)?.RateLimit;
        var globalRateLimit = config.GlobalRateLimit;

        RateLimitResult? serviceResult = null;
        RateLimitResult? globalResult = null;

        if (serviceRateLimit is not null)
        {
            var strategy = _strategyResolver.Resolve(serviceRateLimit.Strategy);
            serviceResult = await strategy.PeekAsync($"{clientId}:{serviceId}", serviceRateLimit, cancellationToken);
        }

        if (globalRateLimit is not null)
        {
            var strategy = _strategyResolver.Resolve(globalRateLimit.Strategy);
            globalResult = await strategy.PeekAsync($"{clientId}:global", globalRateLimit, cancellationToken);
        }

        return CombineResults(serviceResult, globalResult);
    }

    /// <summary>
    /// Retrieves a global rate limit from a short-lived cache to avoid a
    /// <c>GetAll + FirstOrDefault</c> collection scan on every request.
    /// The 30-second TTL keeps the cache fresh enough for admin-edited config.
    /// </summary>
    private async Task<GlobalRateLimit?> GetCachedGlobalLimitAsync(
        string targetId, TargetType targetType, CancellationToken cancellationToken)
    {
        var cacheKey = $"global-limit:{targetId}:{targetType}";
        if (!_cache.TryGetValue(cacheKey, out GlobalRateLimit? cached))
        {
            cached = await _globalRateLimitDatabase.GetByTargetAsync(targetId, targetType, cancellationToken);
            _cache.Set(cacheKey, cached, GlobalLimitCacheTtl);
        }
        return cached;
    }

    private static RateLimitResult Allowed() => new()
    {
        IsAllowed = true,
        RemainingRequests = int.MaxValue
    };

    private static RateLimitResult CombineResults(RateLimitResult? first, RateLimitResult? second)
    {
        if (first is null && second is null)
            return Allowed();
        if (first is null)
            return second!;
        if (second is null)
            return first;

        // If either is denied, return the one with the highest RetryAfterSeconds
        if (!first.IsAllowed || !second.IsAllowed)
        {
            if (!first.IsAllowed && !second.IsAllowed)
            {
                return (first.RetryAfterSeconds ?? 0) >= (second.RetryAfterSeconds ?? 0) ? first : second;
            }
            return !first.IsAllowed ? first : second;
        }

        // Both allowed - return the lower RemainingRequests
        return first.RemainingRequests <= second.RemainingRequests ? first : second;
    }

    private static ClientRateLimit ToClientRateLimit(GlobalRateLimit globalLimit) => new()
    {
        Strategy = globalLimit.Strategy,
        MaxRequests = globalLimit.MaxRequests,
        Window = globalLimit.Window,
        TokensPerRefill = globalLimit.TokensPerRefill
    };
}
