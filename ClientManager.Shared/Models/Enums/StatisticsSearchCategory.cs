using System.Text.Json.Serialization;

namespace ClientManager.Shared.Models.Enums;

/// <summary>
/// Selects which usage axis a statistics timeseries search measures.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatisticsSearchCategory
{
    /// <summary>Service request volume vs rate-limit caps.</summary>
    ServiceRequests,

    /// <summary>Resource pool slot usage vs max slots.</summary>
    ResourcePoolAllocations,

    /// <summary>Resource pool request volume vs rate-limit caps.</summary>
    ResourcePoolRequests,
}
