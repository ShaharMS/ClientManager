using ClientManager.Shared.Contracts.Statistics;
using ClientManager.Shared.Models.Enums;

namespace ClientManager.Shared.Contracts.Storage;

/// <summary>
/// Immutable relative route fragments for the internal storage-facing API.
/// </summary>
/// <remarks>
/// These paths are the cross-host contract between the public API (which calls them) and the
/// storage host (which serves them). They live in shared code so both sides reference one
/// source of truth. Host-specific values such as the base URL, timeouts, and credentials are
/// intentionally excluded; those belong to typed options on each host. All paths are relative
/// and combine with the configured storage base address at call time.
/// </remarks>
public static class StorageApiRoutes
{
    private static string Escape(string value) => Uri.EscapeDataString(value);

    /// <summary>
    /// Routes for client configuration documents and their nested settings.
    /// </summary>
    public static class ClientConfigurations
    {
        private const string Base = "internal/v1/configuration/clients";

        /// <summary>Searches client configurations.</summary>
        public const string Search = Base + "/search";

        /// <summary>Returns the route for a single client configuration by identifier.</summary>
        /// <param name="clientId">The client identifier.</param>
        public static string ById(string clientId) => $"{Base}/{Escape(clientId)}";

        /// <summary>Returns the paged route for a client's service settings.</summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">The page size.</param>
        public static string Services(string clientId, int page, int pageSize) =>
            $"{Base}/{Escape(clientId)}/services?page={page}&pageSize={pageSize}";

        /// <summary>Returns the route for a single client/service settings pair.</summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="serviceId">The service identifier.</param>
        public static string ServiceSettings(string clientId, string serviceId) =>
            $"{Base}/{Escape(clientId)}/services/{Escape(serviceId)}";

        /// <summary>Returns the paged route for a client's resource-pool settings.</summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="page">The 1-based page number.</param>
        /// <param name="pageSize">The page size.</param>
        public static string ResourcePools(string clientId, int page, int pageSize) =>
            $"{Base}/{Escape(clientId)}/resource-pools?page={page}&pageSize={pageSize}";

        /// <summary>Returns the route for a single client/resource-pool settings pair.</summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="poolId">The resource-pool identifier.</param>
        public static string ResourcePoolSettings(string clientId, string poolId) =>
            $"{Base}/{Escape(clientId)}/resource-pools/{Escape(poolId)}";

        /// <summary>Returns the route for a client's global rate limit.</summary>
        /// <param name="clientId">The client identifier.</param>
        public static string GlobalRateLimit(string clientId) =>
            $"{Base}/{Escape(clientId)}/global-rate-limit";
    }

    /// <summary>
    /// Routes for the service catalog.
    /// </summary>
    public static class Services
    {
        private const string Base = "internal/v1/configuration/services";

        /// <summary>Searches services.</summary>
        public const string Search = Base + "/search";

        /// <summary>Returns the route for a single service by identifier.</summary>
        /// <param name="serviceId">The service identifier.</param>
        public static string ById(string serviceId) => $"{Base}/{Escape(serviceId)}";
    }

    /// <summary>
    /// Routes for the resource-pool catalog.
    /// </summary>
    public static class ResourcePools
    {
        private const string Base = "internal/v1/configuration/resource-pools";

        /// <summary>Searches resource pools.</summary>
        public const string Search = Base + "/search";

        /// <summary>Returns the route for a single resource pool by identifier.</summary>
        /// <param name="poolId">The resource-pool identifier.</param>
        public static string ById(string poolId) => $"{Base}/{Escape(poolId)}";
    }

    /// <summary>
    /// Routes for global rate-limit definitions.
    /// </summary>
    public static class GlobalRateLimits
    {
        private const string Base = "internal/v1/configuration/global-rate-limits";

        /// <summary>Searches global rate limits.</summary>
        public const string Search = Base + "/search";

        /// <summary>Returns the route for a single global rate limit by identifier.</summary>
        /// <param name="id">The global rate-limit identifier.</param>
        public static string ById(string id) => $"{Base}/{Escape(id)}";
    }

    /// <summary>
    /// Routes for runtime access checks and resource acquisition/release.
    /// </summary>
    public static class Runtime
    {
        private const string Base = "internal/v1/runtime";

        /// <summary>Checks whether a client may access a target.</summary>
        public const string CheckAccess = Base + "/access/check";

        /// <summary>Acquires a resource slot for a client.</summary>
        public const string AcquireResource = Base + "/resources/acquire";

        /// <summary>Releases a previously acquired resource slot.</summary>
        public const string ReleaseResource = Base + "/resources/release";

        /// <summary>Returns the route describing a client's accessibility.</summary>
        /// <param name="clientId">The client identifier.</param>
        public static string GetAccessibility(string clientId) => $"{Base}/access/{clientId}";
    }

    /// <summary>
    /// Routes for read-model statistics, including time-series and historical usage queries.
    /// </summary>
    public static class Statistics
    {
        private const string Base = "internal/v1/statistics";

        /// <summary>Returns the high-level system overview.</summary>
        public const string Overview = Base + "/overview";

        /// <summary>Searches per-client summary statistics.</summary>
        public const string SearchClientSummaries = Base + "/clients/search";

        /// <summary>Searches per-service statistics.</summary>
        public const string SearchServiceStatistics = Base + "/services/search";

        /// <summary>Searches per-resource-pool statistics.</summary>
        public const string SearchResourcePoolStatistics = Base + "/resource-pools/search";

        /// <summary>Returns global usage statistics.</summary>
        public const string GlobalUsage = Base + "/global-usage";

        /// <summary>Returns the paged client-summary read model.</summary>
        public const string ClientSummaries = Base + "/client-summaries";

        /// <summary>Returns the route for a single client's detailed statistics.</summary>
        /// <param name="clientId">The client identifier.</param>
        public static string ClientDetails(string clientId) => $"{Base}/clients/{Escape(clientId)}";

        /// <summary>Returns the route for a single service's detailed statistics.</summary>
        /// <param name="serviceId">The service identifier.</param>
        public static string ServiceDetails(string serviceId) => $"{Base}/services/{Escape(serviceId)}";

        /// <summary>Returns the route for a single resource pool's detailed statistics.</summary>
        /// <param name="resourcePoolId">The resource-pool identifier.</param>
        public static string ResourcePoolDetails(string resourcePoolId) =>
            $"{Base}/resource-pools/{Escape(resourcePoolId)}";

        /// <summary>
        /// Builds the usage time-series route for one or more targets over an optional range.
        /// </summary>
        /// <param name="filterType">The target type discriminator.</param>
        /// <param name="targetIds">The targeted service or resource-pool identifiers.</param>
        /// <param name="clientIds">Optional client identifiers to scope the result.</param>
        /// <param name="from">Optional inclusive start of the range.</param>
        /// <param name="to">Optional inclusive end of the range.</param>
        /// <param name="granularity">Optional time-bucket granularity.</param>
        public static string UsageTimeSeries(
            TargetType filterType,
            IEnumerable<string> targetIds,
            IEnumerable<string>? clientIds,
            DateTime? from,
            DateTime? to,
            BucketGranularity? granularity) =>
            BuildQueryString(
                Base + "/usage-timeseries",
                filterType,
                targetIds,
                clientIds is null ? null : IdentifierList.Join(clientIds),
                from,
                to,
                granularity);

        /// <summary>
        /// Builds the per-client usage-breakdown route for one or more targets over an optional range.
        /// </summary>
        /// <param name="filterType">The target type discriminator.</param>
        /// <param name="targetIds">The targeted service or resource-pool identifiers.</param>
        /// <param name="clientIds">Optional client identifiers to scope the result.</param>
        /// <param name="from">Optional inclusive start of the range.</param>
        /// <param name="to">Optional inclusive end of the range.</param>
        /// <param name="granularity">Optional time-bucket granularity.</param>
        public static string ClientUsageBreakdown(
            TargetType filterType,
            IEnumerable<string> targetIds,
            IEnumerable<string>? clientIds,
            DateTime? from,
            DateTime? to,
            BucketGranularity? granularity) =>
            BuildQueryString(
                Base + "/client-usage-breakdown",
                filterType,
                targetIds,
                clientIds is null ? null : IdentifierList.Join(clientIds),
                from,
                to,
                granularity);

        /// <summary>
        /// Builds the historical-usage route for one or more targets over a required range.
        /// </summary>
        /// <param name="filterType">The target type discriminator.</param>
        /// <param name="targetIds">The targeted service or resource-pool identifiers.</param>
        /// <param name="clientId">Optional single client identifier to scope the result.</param>
        /// <param name="from">The inclusive start of the range.</param>
        /// <param name="to">The inclusive end of the range.</param>
        /// <param name="granularity">The time-bucket granularity.</param>
        public static string HistoricalUsage(
            TargetType filterType,
            IEnumerable<string> targetIds,
            string? clientId,
            DateTime from,
            DateTime to,
            BucketGranularity granularity) =>
            BuildHistoricalUsageQueryString(filterType, targetIds, clientId, from, to, granularity);

        /// <summary>
        /// Builds the by-client historical-usage route for one or more targets over a required range.
        /// </summary>
        /// <param name="filterType">The target type discriminator.</param>
        /// <param name="targetIds">The targeted service or resource-pool identifiers.</param>
        /// <param name="clientIds">The client identifiers included in the response.</param>
        /// <param name="from">The inclusive start of the range.</param>
        /// <param name="to">The inclusive end of the range.</param>
        /// <param name="granularity">The time-bucket granularity.</param>
        public static string HistoricalUsageByClient(
            TargetType filterType,
            IEnumerable<string> targetIds,
            IEnumerable<string> clientIds,
            DateTime from,
            DateTime to,
            BucketGranularity granularity) =>
            BuildQueryString(
                Base + "/historical-usage/by-client",
                filterType,
                targetIds,
                IdentifierList.Join(clientIds),
                from,
                to,
                granularity);

        private static string BuildQueryString(
            string basePath,
            TargetType filterType,
            IEnumerable<string> targetIds,
            string? clientIds,
            DateTime? from,
            DateTime? to,
            BucketGranularity? granularity)
        {
            var parameters = new List<string>
            {
                $"{StatisticsQueryParameters.FilterType}={Escape(filterType.ToString())}",
                $"{StatisticsQueryParameters.TargetIds}={Escape(IdentifierList.Join(targetIds))}"
            };

            if (!string.IsNullOrWhiteSpace(clientIds))
            {
                parameters.Add($"{StatisticsQueryParameters.ClientIds}={Escape(clientIds)}");
            }

            if (from is not null)
            {
                parameters.Add($"{StatisticsQueryParameters.From}={Escape(from.Value.ToString("O"))}");
            }

            if (to is not null)
            {
                parameters.Add($"{StatisticsQueryParameters.To}={Escape(to.Value.ToString("O"))}");
            }

            if (granularity is not null)
            {
                parameters.Add($"{StatisticsQueryParameters.Granularity}={Escape(granularity.Value.ToString())}");
            }

            return $"{basePath}?{string.Join("&", parameters)}";
        }

        private static string BuildHistoricalUsageQueryString(
            TargetType filterType,
            IEnumerable<string> targetIds,
            string? clientId,
            DateTime from,
            DateTime to,
            BucketGranularity granularity)
        {
            var parameters = new List<string>
            {
                $"{StatisticsQueryParameters.FilterType}={Escape(filterType.ToString())}",
                $"{StatisticsQueryParameters.TargetIds}={Escape(IdentifierList.Join(targetIds))}",
                $"{StatisticsQueryParameters.From}={Escape(from.ToString("O"))}",
                $"{StatisticsQueryParameters.To}={Escape(to.ToString("O"))}",
                $"{StatisticsQueryParameters.Granularity}={Escape(granularity.ToString())}"
            };

            if (!string.IsNullOrWhiteSpace(clientId))
            {
                parameters.Add($"{StatisticsQueryParameters.ClientId}={Escape(clientId)}");
            }

            return $"{Base}/historical-usage?{string.Join("&", parameters)}";
        }
    }

    /// <summary>
    /// Routes for raw metrics scraping endpoints.
    /// </summary>
    public static class Metrics
    {
        private const string Base = "internal/v1/metrics";

        /// <summary>Returns Prometheus-formatted metrics.</summary>
        public const string Prometheus = Base + "/prometheus";

        /// <summary>Returns Grafana dashboard metrics.</summary>
        public const string Grafana = Base + "/grafana";
    }
}
