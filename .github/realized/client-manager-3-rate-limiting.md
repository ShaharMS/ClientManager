# Plan: ClientManager — Step 3: Rate Limiting Engine

> **Status**: ✅ Completed
> **Prerequisite**: [client-manager-2-persistence.md](client-manager-2-persistence.md)
> **Next**: [client-manager-4-resource-allocation.md](client-manager-4-resource-allocation.md)
> **Parent**: [client-manager-overview.md](client-manager-overview.md)

## TL;DR

Implement the three rate limiting strategies (fixed window, sliding window, token bucket) and the `IRateLimitService` that evaluates rate limit policies at three scopes: per-client-per-service, global per-client, and **global per-service/resource-pool** (catch-all aggregate limits). Rate limit configurations are now read from `IClientConfigurationRepository` (nested `ClientRateLimit` objects inside `ClientConfiguration`) rather than from a separate `IRateLimitPolicyRepository`. The service also resolves whether each client contributes to and/or is exempt from global limits, using the client-level and per-service flags from `ClientConfiguration`. All state is stored via `IRateLimitStateStore`, keeping the engine stateless and horizontally scalable.

## Reference Pattern

No existing reference in this project. The rate limiting strategies follow well-known patterns:
- **Fixed window**: Counter resets at the start of each window (e.g. every 60 seconds)
- **Sliding window**: Weighted average of current and previous window counts
- **Token bucket**: Tokens refill at a fixed rate; each request consumes a token

## Steps

### 1. Define the rate limit strategy interface

**File: `ClientManager.DataAccess/Interfaces/IRateLimitStrategy.cs`**

```csharp
using ClientManager.Shared.Models.Entities;
using ClientManager.Api.Interfaces;

namespace ClientManager.DataAccess.Interfaces;

public interface IRateLimitStrategy
{
    Task<RateLimitResult> EvaluateAsync(
        string key,
        ClientRateLimit rateLimit,
        CancellationToken cancellationToken = default);
}
```

> **Note**: The parameter is now `ClientRateLimit` (the nested config object) instead of the old `RateLimitPolicy` entity.

### 2. Implement Fixed Window strategy

**File: `ClientManager.Api/Services/RateLimiting/FixedWindowStrategy.cs`**

Implements `IRateLimitStrategy`:
- Generates a key incorporating the window boundary: `fixed:{key}:{windowNumber}` where `windowNumber = currentTimestamp / windowDuration`
- Calls `IRateLimitStateStore.IncrementAsync` with the rate limit's window duration
- If count > rateLimit.MaxRequests → denied, calculate `RetryAfterSeconds` as time remaining in the current window
- Otherwise → allowed, `RemainingRequests = MaxRequests - count`

```csharp
public class FixedWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateStore _stateStore;

    public FixedWindowStrategy(IRateLimitStateStore stateStore) { ... }
}
```

### 3. Implement Sliding Window strategy

**File: `ClientManager.Api/Services/RateLimiting/SlidingWindowStrategy.cs`**

Implements `IRateLimitStrategy`:
- Uses two keys: current window and previous window
- Current window key: `sliding:{key}:{currentWindowNumber}`
- Previous window key: `sliding:{key}:{currentWindowNumber - 1}`
- Weighted count = `previousCount * (1 - elapsedRatio) + currentCount`
  - `elapsedRatio = (now - windowStart) / windowDuration`
- If weighted count >= MaxRequests → denied
- Increments the current window key

```csharp
public class SlidingWindowStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateStore _stateStore;

    public SlidingWindowStrategy(IRateLimitStateStore stateStore) { ... }
}
```

### 4. Implement Token Bucket strategy

**File: `ClientManager.Api/Services/RateLimiting/TokenBucketStrategy.cs`**

Implements `IRateLimitStrategy`:
- Uses `IRateLimitStateStore` with a custom approach:
  - Stores two values per key: `tokens_remaining` and `last_refill_timestamp`
  - On each call: calculate elapsed time since last refill, add `(elapsed / refillInterval) * tokensPerRefill` tokens (capped at `MaxRequests` which serves as bucket capacity), then decrement by 1
- Key: `bucket:{key}`
- If no tokens remaining → denied, `RetryAfterSeconds = refillInterval / tokensPerRefill` (time to next token)
- This strategy needs two linked values. Use two separate keys:
  - `bucket:{key}:tokens` — token count
  - `bucket:{key}:lastrefill` — timestamp

> **Note**: For the Redis provider, this should ideally use a Lua script for atomicity. The JSON file and MongoDB providers can use their respective locking mechanisms. The strategy itself just calls the state store — the atomicity concern is in the state store implementation.

```csharp
public class TokenBucketStrategy : IRateLimitStrategy
{
    private readonly IRateLimitStateStore _stateStore;

    public TokenBucketStrategy(IRateLimitStateStore stateStore) { ... }
}
```

### 5. Create a strategy resolver

**File: `ClientManager.Api/Services/RateLimiting/RateLimitStrategyResolver.cs`**

Maps `RateLimitStrategy` enum values to `IRateLimitStrategy` implementations.

```csharp
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.RateLimiting;

public class RateLimitStrategyResolver
{
    private readonly IReadOnlyDictionary<RateLimitStrategy, IRateLimitStrategy> _strategies;

    public RateLimitStrategyResolver(
        FixedWindowStrategy fixedWindow,
        SlidingWindowStrategy slidingWindow,
        TokenBucketStrategy tokenBucket)
    {
        _strategies = new Dictionary<RateLimitStrategy, IRateLimitStrategy>
        {
            [RateLimitStrategy.FixedWindow] = fixedWindow,
            [RateLimitStrategy.SlidingWindow] = slidingWindow,
            [RateLimitStrategy.TokenBucket] = tokenBucket,
        };
    }

    public IRateLimitStrategy Resolve(RateLimitStrategy strategy)
    {
        return _strategies[strategy];
    }
}
```

### 6. Implement `IRateLimitService`

**File: `ClientManager.Api/Services/RateLimiting/RateLimitService.cs`**

Implements `IRateLimitService`. Now reads rate limit configs from `IClientConfigurationRepository` instead of separate policy/access-rule repos. Uses NLog structured logging for all rate limit decisions.

```csharp
public class RateLimitService : IRateLimitService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly IClientConfigurationRepository _clientConfigRepository;
    private readonly IGlobalRateLimitRepository _globalRateLimitRepository;
    private readonly RateLimitStrategyResolver _strategyResolver;
}
```

**`CheckAndIncrementAsync(clientId, serviceId)`:**
1. Load `ClientConfiguration` via `IClientConfigurationRepository.GetByIdAsync(clientId)`
2. If not found or not enabled → return allowed (no config = no rate limiting; access control handles denial)
3. Find the per-service rate limit: `config.Services[serviceId]?.RateLimit` (if any)
4. Find the global per-client rate limit: `config.GlobalRateLimit` (if any)
5. Evaluate both (if both exist) using the strategy resolver:
   - Per-service key: `{clientId}:{serviceId}`
   - Global key: `{clientId}:global`
6. Log the decision: `Logger.Debug("Rate limit evaluated | {@Properties}", new { ClientId = clientId, ServiceId = serviceId, Allowed = result.IsAllowed, Remaining = result.RemainingRequests })`
7. Return the most restrictive result:
   - If either is denied → denied (use the one with the highest `RetryAfterSeconds`)
   - If both allowed → allowed with the lower `RemainingRequests`

**`CheckGlobalAndIncrementAsync(clientId)`:**
1. Load `ClientConfiguration`
2. If no `GlobalRateLimit` → return allowed
3. Evaluate using the strategy resolver with key `{clientId}:global`
4. Return result

**`CheckGlobalServiceLimitAsync(clientId, serviceId)`:**
1. Load `ClientConfiguration` to get blanket defaults and per-service overrides:
   - `contributesToGlobal = config.Services[serviceId]?.ContributesToGlobalLimit ?? config.ContributesToGlobalLimits`
   - `exemptFromGlobal = config.Services[serviceId]?.ExemptFromGlobalLimit ?? config.ExemptFromGlobalLimits`
2. Load the `GlobalRateLimit` for this service via `IGlobalRateLimitRepository.GetByTargetAsync(serviceId, GlobalRateLimitTarget.Service)`
3. If no global limit exists → return allowed
4. If `contributesToGlobal` is true → increment the global counter (key: `global:service:{serviceId}`)
5. If `exemptFromGlobal` is true → return allowed regardless of the count
6. Evaluate the global limit using the strategy resolver with the global counter
7. Return result with `IsGlobalLimitHit = true` if denied
8. Log: `Logger.Info("Global service limit checked | {@Properties}", new { ClientId = clientId, ServiceId = serviceId, ContributesToGlobal = contributesToGlobal, ExemptFromGlobal = exemptFromGlobal, Allowed = result.IsAllowed })`

**`CheckGlobalResourcePoolLimitAsync(clientId, resourcePoolId)`:**
Same logic as `CheckGlobalServiceLimitAsync` but uses `GlobalRateLimitTarget.ResourcePool` and key `global:pool:{resourcePoolId}`. For resolving the contribution/exemption flags, use the client-level blanket defaults (there is no per-resource-pool service access setting, so only `config.ContributesToGlobalLimits` / `config.ExemptFromGlobalLimits` apply).

**`CheckWithoutIncrementAsync(clientId, serviceId)`:**
Same logic as `CheckAndIncrementAsync` but calls `IRateLimitStateStore.GetCountAsync` instead of `IncrementAsync`. Used by the accessibility report to get status without side effects.

## Verification

- `dotnet build` succeeds
- `FixedWindowStrategy` correctly resets counters at window boundaries
- `SlidingWindowStrategy` produces weighted counts between the previous and current window
- `TokenBucketStrategy` refills tokens over time and denies when bucket is empty
- `RateLimitService` reads rate limit configs from `ClientConfiguration.Services[serviceId].RateLimit` and `ClientConfiguration.GlobalRateLimit`
- `RateLimitService` evaluates per-service, per-client-global, and global catch-all policies and returns the most restrictive result
- A client with no rate limit configured is always allowed (no config = no restriction)
- Global service limits aggregate traffic from all contributing clients
- A client with `ExemptFromGlobalLimits = true` is never denied by a global limit
- A client with `ContributesToGlobalLimits = false` does not increment global counters
- Per-service overrides on `ServiceAccessSettings` take precedence over client-level blanket defaults
- `CheckWithoutIncrementAsync` returns current status without modifying counters
