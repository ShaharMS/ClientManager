using ClientManager.Shared.Models.Entities;

namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Controls the width of time windows used to aggregate <see cref="UsageBucket"/>s
/// within a <see cref="Entities.UsageSnapshot"/>.
///
/// <para>
///     Finer granularities (e.g. <see cref="Second"/>) give precise, near-real-time visibility
///     but produce many buckets and are therefore retained for a shorter period.
///     Coarser granularities (e.g. <see cref="Day"/>) compress data and are kept longer for
///     trend analysis. The system maintains multiple snapshots per (client, target) combination
///     - one for each granularity - so that dashboards can zoom from seconds to days without
///     re-aggregating.
/// </para>
/// </summary>
public enum BucketGranularity
{
    /// <summary>
    /// 1-second buckets. Highest resolution, shortest retention.
    /// </summary>
    Second,

    /// <summary>
    /// 5-minute buckets. Balances precision with storage cost; suitable for short-term
    /// operational dashboards.
    /// </summary>
    FiveMinute,

    /// <summary>
    /// 1-hour buckets. Lower resolution, longer retention; suitable for medium-term capacity planning.
    /// </summary>
    Hour,

    /// <summary>
    /// 1-day buckets. Coarsest resolution, longest retention; suitable for long-term trend analysis.
    /// </summary>
    Day
}
