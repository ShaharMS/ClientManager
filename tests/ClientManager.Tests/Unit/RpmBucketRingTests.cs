using ClientManager.Shared.Configuration.Storage;

namespace ClientManager.Tests.Unit;

/// <summary>
/// Regression coverage for RPM bucket sizing, retention validation, and flush defaults.
/// </summary>
public sealed class RpmBucketRingTests
{
    [Fact]
    public void RpmOptions_defaults_match_plan()
    {
        var options = new RpmOptions();
        Assert.Equal(1, options.BucketSizeSeconds);
        Assert.Equal(TimeSpan.FromMinutes(10), options.Retention);
        Assert.Equal(100, options.FlushEventCount);
        Assert.Equal(TimeSpan.FromSeconds(1), options.FlushInterval);
        Assert.Equal(TimeSpan.FromSeconds(60), RpmOptions.RpmWindow);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(60)]
    public void BucketSizeSeconds_divides_sixty_second_window(int bucketSizeSeconds)
    {
        var windowSeconds = (int)RpmOptions.RpmWindow.TotalSeconds;
        Assert.Equal(0, windowSeconds % bucketSizeSeconds);
    }

    [Fact]
    public void RpmOptionsValidator_rejects_retention_shorter_than_window()
    {
        var validator = new ClientManager.Api.Services.Storage.RpmOptionsValidator();
        var result = validator.Validate(null, new RpmOptions { Retention = TimeSpan.FromSeconds(30) });
        Assert.False(result.Succeeded);
    }
}
