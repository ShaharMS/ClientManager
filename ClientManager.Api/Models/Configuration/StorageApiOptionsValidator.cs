using Microsoft.Extensions.Options;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Validates <see cref="StorageApiOptions"/> at startup, enforcing the operational
/// invariants that the data-annotation attributes cannot express on their own.
/// </summary>
public sealed class StorageApiOptionsValidator : IValidateOptions<StorageApiOptions>
{
    /// <summary>
    /// Validates the bound storage API options.
    /// </summary>
    /// <param name="name">The named options instance, or null for the default.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>A success or failure result describing every broken invariant.</returns>
    public ValidateOptionsResult Validate(string? name, StorageApiOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            failures.Add($"{StorageApiOptions.SectionName}:BaseUrl must be an absolute URI.");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add($"{StorageApiOptions.SectionName}:Timeout must be positive.");
        }

        if (options.ReadRetryCount < 0)
        {
            failures.Add($"{StorageApiOptions.SectionName}:ReadRetryCount cannot be negative.");
        }

        if (options.InitialRetryDelay < TimeSpan.Zero)
        {
            failures.Add($"{StorageApiOptions.SectionName}:InitialRetryDelay cannot be negative.");
        }

        if (options.FailureThreshold <= 0)
        {
            failures.Add($"{StorageApiOptions.SectionName}:FailureThreshold must be greater than zero.");
        }

        if (options.CircuitBreakDuration <= TimeSpan.Zero)
        {
            failures.Add($"{StorageApiOptions.SectionName}:CircuitBreakDuration must be positive.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
