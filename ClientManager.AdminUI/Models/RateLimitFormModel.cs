using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Models;

public class RateLimitFormModel
{
    public string Id { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public TargetType TargetType { get; set; } = TargetType.Service;
    public RateLimitStrategy Strategy { get; set; }
    public int MaxRequests { get; set; } = 100;
    public double WindowSeconds { get; set; } = 60;
    public int? TokensPerRefill { get; set; }
}
