using ClientManager.Shared.Models.Enums;

namespace ClientManager.AdminUI.Models.Client;

public class ClientFormModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool ContributesToGlobalLimits { get; set; } = true;
    public bool ExemptFromGlobalLimits { get; set; }
}

public class ClientRateLimitEntryModel
{
    public RateLimitStrategy Strategy { get; set; }
    public int MaxRequests { get; set; } = 100;
    public int? TokensPerRefill { get; set; }
}

public class ServiceEntryModel
{
    public string ServiceId { get; set; } = string.Empty;
    public bool IsAllowed { get; set; } = true;
    public bool? ContributesToGlobalLimit { get; set; }
    public bool? ExemptFromGlobalLimit { get; set; }
    public bool HasRateLimit { get; set; }
    public RateLimitStrategy RateLimitStrategy { get; set; }
    public int RateLimitMaxRequests { get; set; } = 100;
    public double RateLimitWindowSeconds { get; set; } = 60;
    public int? RateLimitTokensPerRefill { get; set; }
}
