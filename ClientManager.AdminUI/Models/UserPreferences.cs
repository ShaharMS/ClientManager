using System.Text.Json.Serialization;

namespace ClientManager.AdminUI.Models;

public record UserPreferences
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "light";

    [JsonPropertyName("culture")]
    public string Culture { get; set; } = "";
}
