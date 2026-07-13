namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Configures the global second-bucket RPM ring and per-replica flush batching.
/// </summary>
/// <remarks>
/// <para>
/// Each granted access check increments a UTC second bucket. Buckets are retained for
/// <see cref="Retention"/> and aggregated into a five-minute requests-per-minute average for the
/// dashboard via <see cref="RpmWindow"/>.
/// </para>
/// <para>
/// Per-replica buffering reduces storage writes on the hot path. Setting
/// <see cref="FlushEventCount"/> to <c>1</c> flushes every event immediately.
/// </para>
/// </remarks>
public sealed class RpmOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Rpm";

    /// <summary>
    /// Width of each RPM bucket in seconds. Must divide evenly into the fixed five-minute RPM window.
    /// </summary>
    public int BucketSizeSeconds { get; init; } = 1;

    /// <summary>
    /// How long RPM buckets are retained in storage. Must be at least five minutes.
    /// </summary>
    public TimeSpan Retention { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Flush buffered RPM events when this many events accumulate on a replica. Set to <c>1</c> to disable batching.
    /// </summary>
    public int FlushEventCount { get; init; } = 100;

    /// <summary>
    /// Flush buffered RPM events after this interval elapses on a replica.
    /// </summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Fixed RPM calculation window used by dashboard statistics.
    /// </summary>
    public static readonly TimeSpan RpmWindow = TimeSpan.FromMinutes(5);
}
