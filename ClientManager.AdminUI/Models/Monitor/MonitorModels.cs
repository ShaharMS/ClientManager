namespace ClientManager.AdminUI.Models.Monitor;

public record ClientOption(string Id, string Name);

public record MonitorClientRow(
    string ClientId,
    string ClientName,
    string ServiceName,
    long GrantedLast5Min,
    long DeniedLast5Min,
    int RateLimitCap);

public record ServiceSummaryRow(
    string Id,
    string Name,
    long CurrentUsage,
    int Cap);
