using ClientManager.Shared.Configuration.Storage;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Services.Storage;

/// <summary>
/// Validates <see cref="RpmOptions"/> at startup.
/// </summary>
public sealed class RpmOptionsValidator : IValidateOptions<RpmOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RpmOptions options)
    {
        if (options.BucketSizeSeconds < 1)
        {
            return ValidateOptionsResult.Fail($"{RpmOptions.SectionName}:BucketSizeSeconds must be at least 1.");
        }

        var windowSeconds = (int)RpmOptions.RpmWindow.TotalSeconds;
        if (windowSeconds % options.BucketSizeSeconds != 0)
        {
            return ValidateOptionsResult.Fail($"{RpmOptions.SectionName}:BucketSizeSeconds must divide the five-minute RPM window evenly.");
        }

        if (options.Retention < RpmOptions.RpmWindow)
        {
            return ValidateOptionsResult.Fail($"{RpmOptions.SectionName}:Retention must be at least five minutes.");
        }

        if (options.FlushEventCount < 1)
        {
            return ValidateOptionsResult.Fail($"{RpmOptions.SectionName}:FlushEventCount must be at least 1.");
        }

        if (options.FlushInterval <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{RpmOptions.SectionName}:FlushInterval must be positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
