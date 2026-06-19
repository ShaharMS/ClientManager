namespace ClientManager.Shared.Configuration.Storage;

/// <summary>
/// Configuration for cross-pod background worker coordination.
/// </summary>
public class BackgroundWorkersOptions
{
    public const string SectionName = "BackgroundWorkers";

    /// <summary>
    /// When true, background workers that require cluster-wide exclusivity are skipped
    /// if a leader lock cannot be acquired.
    /// </summary>
    public bool RequireLeaderLock { get; set; } = true;

    /// <summary>
    /// How long a leader lease remains valid before renewal is required.
    /// </summary>
    public TimeSpan LeaderLeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
}
