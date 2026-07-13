using ClientManager.Shared.Models.Entities;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Tests.Unit;

/// <summary>
/// Contract tests for the shared <see cref="RateLimitPolicy"/> model.
/// </summary>
public sealed class RateLimitPolicyTests
{
    [Fact]
    public void Token_bucket_policy_retains_all_algorithm_fields()
    {
        var policy = new RateLimitPolicy
        {
            Strategy = RateLimitStrategy.TokenBucket,
            MaxRequests = 120,
            Window = TimeSpan.FromMinutes(1),
            TokensPerRefill = 30
        };

        Assert.Equal(RateLimitStrategy.TokenBucket, policy.Strategy);
        Assert.Equal(120, policy.MaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(1), policy.Window);
        Assert.Equal(30, policy.TokensPerRefill);
    }

    [Fact]
    public void Fixed_window_policy_does_not_require_refill_fields()
    {
        var policy = new RateLimitPolicy
        {
            Strategy = RateLimitStrategy.FixedWindow,
            MaxRequests = 50,
            Window = TimeSpan.FromSeconds(30)
        };

        Assert.Equal(RateLimitStrategy.FixedWindow, policy.Strategy);
        Assert.Equal(50, policy.MaxRequests);
        Assert.Equal(TimeSpan.FromSeconds(30), policy.Window);
        Assert.Null(policy.TokensPerRefill);
    }
}
