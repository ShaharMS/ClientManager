namespace ClientManager.Shared.Contracts.Statistics;

/// <summary>
/// Canonical query-parameter names used by the statistics endpoints.
/// </summary>
/// <remarks>
/// These names are part of the cross-host contract: the public API binds them from
/// incoming requests and the internal storage routes emit the same names when forwarding
/// the request. Both sides must reference these constants so the names stay synchronized.
/// </remarks>
public static class StatisticsQueryParameters
{
    /// <summary>
    /// The target type discriminator (service or resource pool).
    /// </summary>
    public const string FilterType = "filterType";

    /// <summary>
    /// The comma-separated identifiers of the targeted services or resource pools.
    /// </summary>
    public const string TargetIds = "targetIds";

    /// <summary>
    /// The optional comma-separated client identifiers used to scope the result.
    /// </summary>
    public const string ClientIds = "clientIds";

    /// <summary>
    /// A single optional client identifier used to scope the result.
    /// </summary>
    public const string ClientId = "clientId";

    /// <summary>
    /// The inclusive start of the requested time range (UTC, ISO 8601).
    /// </summary>
    public const string From = "from";

    /// <summary>
    /// The inclusive end of the requested time range (UTC, ISO 8601).
    /// </summary>
    public const string To = "to";

    /// <summary>
    /// The time-bucket granularity for aggregated results.
    /// </summary>
    public const string Granularity = "granularity";
}
