namespace ClientManager.AdminUI.Models;

public record UserPreferences
{
    public string Theme { get; set; } = "light";
    public string DefaultTimeRange { get; set; } = "1h";
    public string DefaultPollingInterval { get; set; } = "10s";
    public string DefaultAxisScale { get; set; } = "Linear";
}
