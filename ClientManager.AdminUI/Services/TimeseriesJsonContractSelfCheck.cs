using System.Text.Json;
using ClientManager.Shared.Models.Enums;
using ClientManager.Shared.Models.Responses;

namespace ClientManager.AdminUI.Services;

/// <summary>ponytail: guards the API's string-enum timeseries response contract.</summary>
internal static class TimeseriesJsonContractSelfCheck
{
    private const string ResponseJson =
        """{"searchCategory":"ServiceRequests","targetType":"Service","sourceGranularity":"OneMinute","targets":[]}""";

    internal static void Run()
    {
        foreach (var category in Enum.GetValues<StatisticsSearchCategory>())
        {
            var response = JsonSerializer.Deserialize<TimeseriesSearchResponse>(
                ResponseJson.Replace("ServiceRequests", category.ToString(), StringComparison.Ordinal),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (response?.SearchCategory != category)
            {
                throw new InvalidOperationException("Timeseries string-enum deserialization failed.");
            }
        }
    }
}
