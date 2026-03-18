using ClientManager.Api.Interfaces;
using ClientManager.DataAccess.Interfaces;
using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;
using Microsoft.Extensions.Logging;

namespace ClientManager.Api.Services.RateLimiting;

/// <summary>
/// Evaluates rate limit policies at per-client, per-service, and global scopes.
/// Reads rate limit configurations from <see cref="IClientConfigurationRepository"/>
/// and global limits from <see cref="IGlobalRateLimitRepository"/>.
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly ILogger<RateLimitService> _logger;
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IGlobalRateLimitRepository _globalRateLimitRepository;
    private readonly RateLimitStrategyResolver _strategyResolver;

    /// <summary>
    /// Initializes a new instance of <see cref="RateLimitService"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="clientConfigRepository">Repository for client configurations.</param>
    /// <param name="globalRateLimitRepository">Repository for global rate limits.</param>
    /// <param name="strategyResolver">Resolver for rate limit strategy implementations.</param>
    public RateLimitService(
        ILogger<RateLimitService> logger,
        IClientConfigurationRepository clientConfigRepository,
        IGlobalRateLimitRepository globalRateLimitRepository,
        RateLimitStrategyResolver strategyResolver)
    {
        _logger = logger;
        _clientConfigRepository = clientConfigRepository;
        _globalRateLimitRepository = globalRateLimitRepository;
        _strategyResolver = strategyResolver;
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

        _logger.LogDebug("Rate limit evaluated | ClientId={ClientId}, ServiceId={ServiceId}, Allowed={Allowed}, Remaining={Remaining}",
            clientId, serviceId, result.IsAllowed, result.RemainingRequests);

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
            serviceId, GlobalRateLimitTarget.Service, cancellationToken);

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
            }
        }

        _logger.LogInformation("Global service limit checked | ClientId={ClientId}, ServiceId={ServiceId}, ContributesToGlobal={ContributesToGlobal}, ExemptFromGlobal={ExemptFromGlobal}, Allowed={Allowed}",
            clientId, serviceId, contributesToGlobal, exemptFromGlobal, result.IsAllowed);

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
            resourcePoolId, GlobalRateLimitTarget.ResourcePool, cancellationToken);

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
            }
        }

        _logger.LogInformation("Global resource pool limit checked | ClientId={ClientId}, ResourcePoolId={ResourcePoolId}, ContributesToGlobal={ContributesToGlobal}, ExemptFromGlobal={ExemptFromGlobal}, Allowed={Allowed}",
            clientId, resourcePoolId, contributesToGlobal, exemptFromGlobal, result.IsAllowed);

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

        // Both allowed — return the lower RemainingRequests
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
