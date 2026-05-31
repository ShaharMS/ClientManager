using Asp.Versioning;
using Microsoft.Extensions.Options;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Validates <see cref="ApiVersioningSettings"/> at startup, ensuring the configured
/// default version is a parseable API version.
/// </summary>
public sealed class ApiVersioningSettingsValidator : IValidateOptions<ApiVersioningSettings>
{
    /// <summary>
    /// Validates the bound API versioning settings.
    /// </summary>
    /// <param name="name">The named options instance, or null for the default.</param>
    /// <param name="options">The settings instance to validate.</param>
    /// <returns>A success or failure result describing any broken invariant.</returns>
    public ValidateOptionsResult Validate(string? name, ApiVersioningSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.DefaultVersion))
        {
            return ValidateOptionsResult.Fail(
                $"{ApiVersioningSettings.SectionName}:DefaultVersion must be provided.");
        }

        if (!ApiVersionParser.Default.TryParse(options.DefaultVersion, out _))
        {
            return ValidateOptionsResult.Fail(
                $"{ApiVersioningSettings.SectionName}:DefaultVersion '{options.DefaultVersion}' is not a valid API version.");
        }

        return ValidateOptionsResult.Success;
    }
}
