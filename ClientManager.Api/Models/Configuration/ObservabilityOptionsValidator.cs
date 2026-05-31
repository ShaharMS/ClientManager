using Microsoft.Extensions.Options;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Validates <see cref="ObservabilityOptions"/> at startup, ensuring a configured OTLP
/// endpoint is an absolute URI when one is supplied.
/// </summary>
public sealed class ObservabilityOptionsValidator : IValidateOptions<ObservabilityOptions>
{
    /// <summary>
    /// Validates the bound observability options.
    /// </summary>
    /// <param name="name">The named options instance, or null for the default.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>A success or failure result describing any broken invariant.</returns>
    public ValidateOptionsResult Validate(string? name, ObservabilityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            return ValidateOptionsResult.Success;
        }

        return Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{ObservabilityOptions.SectionName}:OtlpEndpoint must be an absolute URI when provided.");
    }
}
