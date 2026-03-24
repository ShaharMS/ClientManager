using System.Diagnostics;
using ClientManager.Api.Interfaces;
using ClientManager.Api.Services.Instrumentation;
using ClientManager.DataAccess.Databases.Interfaces;
using ClientManager.Shared.Logging;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.RateLimiting;

/// <summary>
/// Evaluates rate limit policies at per-client, per-service, and global scopes.
/// Reads rate limit configurations from <see cref="IClientConfigurationRepository"/>
/// and global limits from <see cref="IGlobalRateLimitRepository"/>.
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly IAppLogger<RateLimitService> _logger;
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IGlobalRateLimitRepository _globalRateLimitRepository;
    private readonly RateLimitStrategyResolver _strategyResolver;
    private readonly ClientManagerMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="clientConfigRepository">Repository for client configurations.</param>
    /// <param name="globalRateLimitRepository">Repository for global rate limits.</param>
    /// <param name="strategyResolver">Resolver for rate limit strategy implementations.</param>
    /// <param name="metrics">The metrics instrumentation instance.</param>
    public RateLimitService(
        IAppLogger<RateLimitService> logger,
        IClientConfigurationRepository clientConfigRepository,
        IGlobalRateLimitRepository globalRateLimitRepository,
        RateLimitStrategyResolver strategyResolver,
        ClientManagerMetrics metrics)
    {
        _logger = logger;
        _clientConfigRepository = clientConfigRepository;
        _globalRateLimitRepository = globalRateLimitRepository;
        _strategyResolver = strategyResolver;
        _metrics = metrics;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckAndIncrementAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
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
                { "clientId", clientId },
                { "serviceId", serviceId }
            });
        else
            _metrics.RateLimitDenied.Add(1, new TagList
            {
                { "clientId", clientId },
                { "serviceId", serviceId }
            });

        _logger.Debug("Rate limit evaluated", new { ClientId = clientId, ServiceId = serviceId, Allowed = result.IsAllowed, Remaining = result.RemainingRequests });

        return result;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckGlobalAndIncrementAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
        if (config?.GlobalRateLimit is null)
        {
            return Allowed();
        }

        var strategy = _strategyResolver.Resolve(config.GlobalRateLimit.Strategy);
        return await strategy.EvaluateAsync($"{clientId}:global", config.GlobalRateLimit, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckGlobalServiceLimitAsync(
        string clientId,
        string serviceId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            return Allowed();
        }

        var serviceSettings = config.Services.GetValueOrDefault(serviceId);
        var contributesToGlobal = serviceSettings?.ContributesToGlobalLimit ?? config.ContributesToGlobalLimits;
        var exemptFromGlobal = serviceSettings?.ExemptFromGlobalLimit ?? config.ExemptFromGlobalLimits;

        var globalLimit = await _globalRateLimitRepository.GetByTargetAsync(
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
                    { "clientId", clientId },
                    { "serviceId", serviceId }
                });
            }
        }

        _logger.Info("Global service limit checked", new { ClientId = clientId, ServiceId = serviceId, ContributesToGlobal = contributesToGlobal, ExemptFromGlobal = exemptFromGlobal, Allowed = result.IsAllowed });

        return result;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> CheckGlobalResourcePoolLimitAsync(
        string clientId,
        string resourcePoolId,
        CancellationToken cancellationToken = default)
    {
        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
        if (config is null)
        {
            return Allowed();
        }

        var contributesToGlobal = config.ContributesToGlobalLimits;
        var exemptFromGlobal = config.ExemptFromGlobalLimits;

        var globalLimit = await _globalRateLimitRepository.GetByTargetAsync(
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
                    { "clientId", clientId },
                    { "resourcePoolId", resourcePoolId }
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
        var config = await _clientConfigRepository.GetByIdAsync(clientId, cancellationToken);
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
