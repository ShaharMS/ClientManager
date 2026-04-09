using ClientManager.Shared.Models.Enums;

namespace ClientManager.Api.Services.InternalClients;

internal static class StorageApiRoutes
{
    internal static class ClientConfigurations
    {
        private const string Base = "internal/v1/configuration/clients";

        public const string Search = Base + "/search";

        public static string ById(string clientId) => $"{Base}/{Uri.EscapeDataString(clientId)}";

        public static string Services(string clientId, int page, int pageSize) =>
            $"{Base}/{Uri.EscapeDataString(clientId)}/services?page={page}&pageSize={pageSize}";

        public static string ServiceSettings(string clientId, string serviceId) =>
            $"{Base}/{Uri.EscapeDataString(clientId)}/services/{Uri.EscapeDataString(serviceId)}";

        public static string ResourcePools(string clientId, int page, int pageSize) =>
            $"{Base}/{Uri.EscapeDataString(clientId)}/resource-pools?page={page}&pageSize={pageSize}";

        public static string ResourcePoolSettings(string clientId, string poolId) =>
            $"{Base}/{Uri.EscapeDataString(clientId)}/resource-pools/{Uri.EscapeDataString(poolId)}";

        public static string GlobalRateLimit(string clientId) =>
            $"{Base}/{Uri.EscapeDataString(clientId)}/global-rate-limit";
    }

    internal static class Services
    {
        private const string Base = "internal/v1/configuration/services";

        public const string Search = Base + "/search";

        public static string ById(string serviceId) => $"{Base}/{Uri.EscapeDataString(serviceId)}";
    }

    internal static class ResourcePools
    {
        private const string Base = "internal/v1/configuration/resource-pools";

        public const string Search = Base + "/search";

        public static string ById(string poolId) => $"{Base}/{Uri.EscapeDataString(poolId)}";
    }

    internal static class GlobalRateLimits
    {
        private const string Base = "internal/v1/configuration/global-rate-limits";

        public const string Search = Base + "/search";

        public static string ById(string id) => $"{Base}/{Uri.EscapeDataString(id)}";
    }

    internal static class Runtime
    {
        private const string Base = "internal/v1/runtime";

        public const string CheckAccess = Base + "/access/check";
        public const string AcquireResource = Base + "/resources/acquire";
        public const string ReleaseResource = Base + "/resources/release";

        public static string GetAccessibility(string clientId) => $"{Base}/access/{clientId}";
    }

    internal static class Statistics
    {
        private const string Base = "internal/v1/statistics";

        public const string Overview = Base + "/overview";
        public const string SearchClientSummaries = Base + "/clients/search";
        public const string SearchServiceStatistics = Base + "/services/search";
        public const string SearchResourcePoolStatistics = Base + "/resource-pools/search";
        public const string GlobalUsage = Base + "/global-usage";
        public const string ClientSummaries = Base + "/client-summaries";

        public static string ClientDetails(string clientId) => $"{Base}/clients/{Uri.EscapeDataString(clientId)}";

        public static string ServiceDetails(string serviceId) => $"{Base}/services/{Uri.EscapeDataString(serviceId)}";

        public static string ResourcePoolDetails(string resourcePoolId) => $"{Base}/resource-pools/{Uri.EscapeDataString(resourcePoolId)}";

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
                clientIds is null ? null : string.Join(',', clientIds),
                from,
                to,
                granularity);

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
                clientIds is null ? null : string.Join(',', clientIds),
                from,
                to,
                granularity);

        public static string HistoricalUsage(
            TargetType filterType,
            IEnumerable<string> targetIds,
            string? clientId,
            DateTime from,
            DateTime to,
            BucketGranularity granularity) =>
            BuildHistoricalUsageQueryString(filterType, targetIds, clientId, from, to, granularity);

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
                $"filterType={Uri.EscapeDataString(filterType.ToString())}",
                $"targetIds={Uri.EscapeDataString(string.Join(',', targetIds))}"
            };

            if (!string.IsNullOrWhiteSpace(clientIds))
                parameters.Add($"clientIds={Uri.EscapeDataString(clientIds)}");
            if (from is not null)
                parameters.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
            if (to is not null)
                parameters.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
            if (granularity is not null)
                parameters.Add($"granularity={Uri.EscapeDataString(granularity.Value.ToString())}");

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
                $"filterType={Uri.EscapeDataString(filterType.ToString())}",
                $"targetIds={Uri.EscapeDataString(string.Join(',', targetIds))}",
                $"from={Uri.EscapeDataString(from.ToString("O"))}",
                $"to={Uri.EscapeDataString(to.ToString("O"))}",
                $"granularity={Uri.EscapeDataString(granularity.ToString())}"
            };

            if (!string.IsNullOrWhiteSpace(clientId))
                parameters.Add($"clientId={Uri.EscapeDataString(clientId)}");

            return $"{Base}/historical-usage?{string.Join("&", parameters)}";
        }
    }

    internal static class Metrics
    {
        private const string Base = "internal/v1/metrics";

        public const string Prometheus = Base + "/prometheus";
        public const string Grafana = Base + "/grafana";
    }
}