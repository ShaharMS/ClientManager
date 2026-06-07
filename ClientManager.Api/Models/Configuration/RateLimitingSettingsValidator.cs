using Microsoft.Extensions.Options;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Validates <see cref="RateLimitingSettings"/> at startup.
/// </summary>
public sealed class RateLimitingSettingsValidator : IValidateOptions<RateLimitingSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RateLimitingSettings options)
    {
        if (options.WindowAlignmentAnchor is not { } anchor)
        {
            return ValidateOptionsResult.Success;
        }

        if (anchor < TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail(
                $"{RateLimitingSettings.SectionName}:WindowAlignmentAnchor must not be negative.");
        }

        if (anchor > TimeSpan.FromDays(1))
        {
            return ValidateOptionsResult.Fail(
                $"{RateLimitingSettings.SectionName}:WindowAlignmentAnchor must not exceed 24 hours.");
        }

        return ValidateOptionsResult.Success;
    }
}
