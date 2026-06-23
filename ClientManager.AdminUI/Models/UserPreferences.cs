using System.Text.Json.Serialization;

namespace ClientManager.AdminUI.Models;

public record UserPreferences
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "light";

    [JsonPropertyName("culture")]
    public string Culture { get; set; } = "";

    [JsonPropertyName("defaultTimeRange")]
    public string DefaultTimeRange { get; set; } = "1h";

    [JsonPropertyName("defaultPollingInterval")]
    public string DefaultPollingInterval { get; set; } = "10s";

    [JsonPropertyName("defaultAxisScale")]
    public string DefaultAxisScale { get; set; } = "Linear";
}
