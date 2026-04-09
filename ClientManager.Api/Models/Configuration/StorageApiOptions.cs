using System.ComponentModel.DataAnnotations;

namespace ClientManager.Api.Models.Configuration;

/// <summary>
/// Outbound connection settings for the internal storage-facing API.
/// </summary>
public sealed class StorageApiOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "StorageApi";

    /// <summary>
    /// The absolute base URL for the internal storage-facing API.
    /// </summary>
    [Required]
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Request timeout for internal storage API calls.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Number of additional retry attempts for idempotent internal reads.
    /// </summary>
    public int ReadRetryCount { get; init; } = 2;

    /// <summary>
    /// Initial delay between idempotent read retries.
    /// </summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Consecutive failure threshold before the internal circuit opens.
    /// </summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>
    /// How long to fast-fail once the internal circuit opens.
    /// </summary>
    public TimeSpan CircuitBreakDuration { get; init; } = TimeSpan.FromSeconds(15);
}